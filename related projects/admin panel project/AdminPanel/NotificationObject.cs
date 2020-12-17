using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows;

namespace AdminPanel
{
    public class NotificationObject : INotifyPropertyChanged
    {
        protected bool SetProperty<T>(ref T storage, T value, Expression<Func<T>> action)
        {
            if (Equals(storage, value))
                return false;
            storage = value;
            RaisePropertyChanged(action);
            return true;
        }

        protected void RaisePropertyChanged<T>(Expression<Func<T>> action)
        {
            var propertyName = GetPropertyName(action);
            RaisePropertyChanged(propertyName);
        }

        private static string GetPropertyName<T>(Expression<Func<T>> action)
        {
            var expression = (MemberExpression)action.Body;
            var propertyName = expression.Member.Name;
            return propertyName;
        }

        private void RaisePropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void AddElement<T>(T element, IList<T> collection)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                collection.Insert(0, element);
            });
        }

        protected void AddDictionaryElement<T,TF>(T elementName, TF elementValue, IDictionary<T,TF> collection)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                collection.Add(elementName, elementValue);
            });
        }

        protected void RemoveElement<T>(T element, IList<T> collection)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                collection.Remove(element);
            });
        }

        protected void Clear<T>(IList<T> collection)
        {
            Application.Current.Dispatcher.Invoke(collection.Clear);
        }
    }
}
