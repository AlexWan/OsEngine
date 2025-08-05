using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OsEngine.Attributes
{
    public class AttributeInitializer
    {
        private readonly BotPanel _bot;
        private readonly Aindicator _indicator;

        private readonly BindingFlags _declaredFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance;
        private readonly BindingFlags _flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private List<ParameterElementAttribute> _parametersAttributes = new();

        public AttributeInitializer(Aindicator indicator) => _indicator = indicator;
        public AttributeInitializer(BotPanel bot) => _bot = bot;

        private enum ComponentType
        {
            Bot,
            Indicator
        }

        public void InitAttributes()
        {
            if (_bot != null)
                InitBotAttribute(_bot.GetType(), _bot);
            else if (_indicator != null)
                InitIndicatorAttributes(_indicator.GetType(), _indicator);
        }

        public void InitIndicatorAttributes(Type type, object instance, string tabControlName = null)
        {
            List<AttributeMember> attributeMembers = GetMembers(type, instance, tabControlName);

            for (int i = 0; i < attributeMembers.Count; i++)
                Bind(attributeMembers[i], ComponentType.Indicator);
        }

        public void InitBotAttribute(Type type, object instance, string tabControlName = null)
        {
            List<AttributeMember> attributeMembers = GetMembers(type, instance, tabControlName);

            for (int i = 0; i < attributeMembers.Count; i++)
                Bind(attributeMembers[i], ComponentType.Bot);
        }

        private void Bind(AttributeMember member, ComponentType componentType)
        {
            Type type = member.Type;
            var customAttributes = member.CustomAttributes;

            foreach (var attribute in customAttributes)
            {
                switch (componentType)
                {
                    case ComponentType.Bot:
                        attribute.BindToBot(_bot, member, this);
                        break;
                    case ComponentType.Indicator:
                        attribute.BindToIndicator(_indicator, member, this);
                        break;
                    default:
                        break;
                }
            }
        }

        private List<AttributeMember> GetMembers(Type type, object instance, string tabControlName = null)
        {
            var fields = type.GetFields(_flags);
            var autoProperties = GetAutoPropertiesRecursively(type);
            var methods = type.GetMethods(_flags).Where(m => !m.Name.StartsWith("get_")
                                                         && !m.Name.StartsWith("set_")
                                                         && m.GetParameters().Length == 0);

            var members = fields.Concat<MemberInfo>(autoProperties).Concat(methods);

            return CreateMany(members, instance, tabControlName);
        }

        private List<PropertyInfo> GetAutoPropertiesRecursively(Type type)
        {
            Type tempType = type;
            Type parentBot = typeof(BotPanel);
            Type parentIndicator = typeof(Aindicator);
            List<PropertyInfo> autoProperties = new List<PropertyInfo>();

            while (tempType != null && tempType != parentBot && tempType != parentIndicator)
            {
                var properties = tempType.GetProperties(_flags).Where(IsAutoProperty);
                autoProperties.AddRange(properties);
                tempType = tempType.BaseType;
            }

            return autoProperties;
        }

        private bool IsAutoProperty(PropertyInfo property)
        {
            if (property.SetMethod == null)
                return false;

            var name = $"<{property.Name}>k__BackingField";
            var backingField = property.DeclaringType.GetField(name, _declaredFlags);
            return backingField != null;
        }

        private List<AttributeMember> CreateMany(IEnumerable<MemberInfo> members, object instance, string tabControlName = null)
        {
            List<AttributeMember> instances = new();

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
                else if (member is PropertyInfo property)
                    instances.Add(new AttributeMember(property, property.PropertyType, customAttributes, instance, property.SetValue));
                else if (member is MethodInfo method)
                    instances.Add(new AttributeMember(method, null, customAttributes, instance, methodInvocation: () => method.Invoke(instance, null)));
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
            string[] parts = fieldName.Split('_', StringSplitOptions.RemoveEmptyEntries);

            var resultParts = new List<string>();

            foreach (string part in parts)
            {
                if (!string.IsNullOrEmpty(part))
                {
                    string spacedPart = AddSpacesToCamelCase(part);
                    // Делаем каждое слово с заглавной буквы
                    resultParts.Add(CapitalizeFirstLetter(spacedPart));
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
                       (i < input.Length - 1 && char.IsLower(input[i + 1]))))
                    {
                        result.Append(' ');
                    }
                }
                result.Append(input[i]);
            }

            return result.ToString();
        }

        private string CapitalizeFirstLetter(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            if (input.Length > 0)
                return char.ToUpper(input[0]) + (input.Length > 1 ? input.Substring(1) : "");

            return input;
        }

        public struct AttributeMember
        {
            public object Instance { get; private set; }
            public ParameterElementAttribute[] CustomAttributes { get; private set; }
            public Type Type { get; private set; }
            public MemberTypes MemberType { get; private set; }
            public string Name { get; private set; }

            private Action<object, object> _setValue;
            private Action _methodInvokation;

            public readonly bool IsParameter => Type?.GetInterface(nameof(IIStrategyParameter)) != null
                                             || Type?.BaseType == typeof(IndicatorParameter);

            public readonly bool IsMethod => MemberType == MemberTypes.Method;

            public AttributeMember(MemberInfo member, Type type, ParameterElementAttribute[] customAttributes, object instance, Action<object, object> setValue = null, Action methodInvocation = null)
            {
                _setValue = setValue;
                _methodInvokation = methodInvocation;
                Type = type;
                Name = member.Name;
                CustomAttributes = customAttributes;
                MemberType = member.MemberType;
                Instance = instance;
            }

            public T GetAttribute<T>() => CustomAttributes.OfType<T>().FirstOrDefault();

            public void InvokeIfMethod()
            {
                if (MemberType != MemberTypes.Method)
                    throw new InvalidOperationException("MemberType is not Method");

                _methodInvokation?.Invoke();
            }

            public void SetValue(object value)
            {
                if (value == null)
                    return;

                _setValue?.Invoke(Instance, value);
            }
        }
    }
}
