using OsEngine.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OsEngine.Attributes
{
    public class AttributeInitializer
    {
        private readonly Aindicator _indicator;

        private readonly BindingFlags _flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance;

        private List<ParameterElementAttribute> _parametersAttributes = new List<ParameterElementAttribute>();

        public AttributeInitializer(Aindicator indicator) => _indicator = indicator;

        public void InitAttributes() => InitIndicatorAttributes(_indicator.GetType(), _indicator);

        public void InitIndicatorAttributes(Type type, object instance, string tabControlName = null)
        {
            List<AttributeMember> attributeMembers = GetMembers(type, instance, tabControlName);

            for (int i = 0; i < attributeMembers.Count; i++)
                Bind(attributeMembers[i]);
        }

        private void Bind(AttributeMember member)
        {
            Type type = member.Type;
            var customAttributes = member.CustomAttributes;

            foreach (var attribute in customAttributes)
            {
                attribute.BindToIndicator(_indicator, member, this);
            }
        }

        private List<AttributeMember> GetMembers(Type type, object instance, string tabControlName = null)
        {
            var fields = type.GetFields(_flags);

            return CreateMany(fields, instance, tabControlName);
        }

        private List<AttributeMember> CreateMany(IEnumerable<MemberInfo> members, object instance, string tabControlName = null)
        {
            List<AttributeMember> instances = new List<AttributeMember>();

            foreach (var member in members)
            {
                ParameterElementAttribute[] customAttributes = member.GetCustomAttributes<ParameterElementAttribute>().ToArray();

                if (customAttributes.Length == 0)
                    continue;

                foreach (var attribute in customAttributes)
                {
                    if (string.IsNullOrEmpty(attribute.Name))
                        attribute.Name = ConvertToReadable(member.Name);

                    ValidateParameterName(attribute);

                    if (!string.IsNullOrEmpty(tabControlName) && string.IsNullOrEmpty(attribute.TabControlName))
                        attribute.TabControlName = tabControlName;
                }

                if (member is FieldInfo field)
                    instances.Add(new AttributeMember(field, field.FieldType, customAttributes, instance, field.SetValue));
            }
            return instances;
        }

        private void ValidateParameterName(ParameterElementAttribute attribute)
        {
            _parametersAttributes.Add(attribute);

            bool hasDublicate;

            do
            {
                hasDublicate = false;

                for (int i = 0; i < _parametersAttributes.Count; i++)
                {
                    if (attribute != _parametersAttributes[i] && attribute.Name == _parametersAttributes[i].Name)
                    {
                        hasDublicate = true;
                        attribute.IncrementName();
                    }
                }
            }
            while (hasDublicate);
        }

        private string ConvertToReadable(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException("Field name can not be null or empty");

            if (fieldName.StartsWith("m_"))
                fieldName = fieldName.Remove(0, 2);

            fieldName = fieldName.TrimStart('_');

            // Разбиваем по подчеркиваниям для snake_case
            string[] parts = fieldName.Split(new char[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

            var resultParts = new List<string>();

            foreach (string part in parts)
            {
                if (!string.IsNullOrEmpty(part))
                {
                    string spacedPart = AddSpacesToCamelCase(part);
                    // Делаем каждое слово с заглавной буквы
                    resultParts.Add(CapitalizeEachWord(spacedPart));
                }
            }

            return string.Join(" ", resultParts);
        }

        private string AddSpacesToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new StringBuilder();
            result.Append(input[0]);

            for (int i = 1; i < input.Length; i++)
            {
                if (char.IsUpper(input[i]))
                {
                    if (i > 0 && (char.IsLower(input[i - 1]) ||
                       i < input.Length - 1 && char.IsLower(input[i + 1])))
                    {
                        result.Append(' ');
                    }
                }
                result.Append(input[i]);
            }

            return result.ToString();
        }

        private string CapitalizeEachWord(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string[] words = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) +
                              (words[i].Length > 1 ? words[i].Substring(1).ToLower() : "");
                }
            }

            return string.Join(" ", words);
        }

        public struct AttributeMember
        {
            public object Instance { get; private set; }
            public ParameterElementAttribute[] CustomAttributes { get; private set; }
            public Type Type { get; private set; }
            public MemberTypes MemberType { get; private set; }
            public string Name { get; private set; }

            private Action<object, object> _setValue;

            public AttributeMember(MemberInfo member, Type type, ParameterElementAttribute[] customAttributes, object instance, Action<object, object> setValue = null)
            {
                _setValue = setValue;
                Type = type;
                Name = member.Name;
                CustomAttributes = customAttributes;
                MemberType = member.MemberType;
                Instance = instance;
            }

            public void SetValue(object value)
            {
                if(value == null)
                    return;

                _setValue?.Invoke(Instance, value);
            }
        }
    }
}
