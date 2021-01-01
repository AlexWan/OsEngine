using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace AdminSlave
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
    }
}
