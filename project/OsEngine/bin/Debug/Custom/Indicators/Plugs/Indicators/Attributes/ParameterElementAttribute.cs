using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OsEngine.Indicators;

namespace OsEngine.Attributes
{
    public abstract class ParameterElementAttribute : Attribute
    {
        public const char Separator = ':';
        public const string NumberFormatWrapper = "[]";

        protected string _name;

        public string Data { get; protected set; } = string.Empty;
        public string TabControlName { get; set; }
        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrEmpty(value))
                    return;

                _name = value;
            }
        }

        public virtual void BindToIndicator(Aindicator indicator, AttributeInitializer.AttributeMember member, AttributeInitializer initializer)
        {
            throw new NotImplementedException($"The method {nameof(BindToIndicator)} is not implemented");
        }

        public void IncrementName()
        {
            // Паттерн для поиска уникализатора вида [число] в конце строки
            var regex = new Regex($@"\{NumberFormatWrapper[0]}(\d+)\{NumberFormatWrapper[1]}$");
            var match = regex.Match(_name);

            if (match.Success)
            {
                // Извлекаем текущее число
                int currentNumber = int.Parse(match.Groups[1].Value);
                int newNumber = currentNumber + 1;

                // Заменяем старый уникализатор на новый
                _name = regex.Replace(_name, Wrap(newNumber));
            }
            else
            {
                // Уникализатор не найден — добавляем [1]
                _name = $"{_name}{Wrap(1)}";
            }
        }

        private string Wrap(object text) => $"{NumberFormatWrapper[0]}{text}{NumberFormatWrapper[1]}";

        protected string ToString(params object[] values)
        {
            if (values == null || values.Length == 0)
                throw new InvalidOperationException("values length must be more than zero.");

            string result = $"{values[0]}";

            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] is IEnumerable<object> collection)
                {
                    foreach (var value in collection)
                        result += $"{Separator}{value}";
                }
                else
                {
                    result += $"{Separator}{values[i]}";
                }
            }

            return result;
        }
    }
}
