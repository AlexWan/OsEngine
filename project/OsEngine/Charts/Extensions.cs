using System;
using System.Reflection;
using System.Windows.Forms.DataVisualization.Charting;

namespace OsEngine.Charts
{
    public static class Extensions
    {
        private static readonly PropertyInfo CommonPropertyInfo = typeof(DataPointCollection).GetProperty("Common", BindingFlags.NonPublic | BindingFlags.Instance);
        private static PropertyInfo ChartPicturePropertyInfo;
        private static MethodInfo ChartPictureTypeResetMethodInfo;
            
        /// <summary>
        /// Быстрая очистка коллекции точек. 
        /// </summary>
        /// <remarks>
        /// По сути перекладка оригинальных исходников из
        /// https://github.com/dotnet/winforms-datavisualization/blob/main/src/System.Windows.Forms.DataVisualization/DataManager/DataPoint.cs#L1713
        /// и https://github.com/dotnet/winforms-datavisualization/blob/main/src/System.Windows.Forms.DataVisualization/General/BaseCollections.cs#L134,
        /// только вместо this.RemoveItem(0) делается this.RemoveItem(this.Length -1), т.е. удаляется с конца, ибо удаление с начала каждый вызов делает
        /// ресайз, и на чартах от 10000 точек это начинает очень сильно тормозить, ибо сложность в лучшем случае O(N!).
        /// Т.к. нужные свойства скрыты, приходится делать это через рефлексию.
        /// </remarks>
        public static void ClearFast(this DataPointCollection dataPointCollection)
        {
            if (dataPointCollection == null)
                return;

            // вообще тут еще проверка на .items.IsReadOnly с выбросом исключения, но тут такого быть не может, поэтому можно пропустить 

            var commonPropertyValue = CommonPropertyInfo.GetValue(dataPointCollection);
            if (commonPropertyValue != null)
            {
                if (ChartPicturePropertyInfo == null)
                    ChartPicturePropertyInfo = commonPropertyValue.GetType().GetProperty("ChartPicture", BindingFlags.NonPublic | BindingFlags.Instance);

                var chartPictureValue = ChartPicturePropertyInfo.GetValue(commonPropertyValue);
                if (chartPictureValue != null)
                {
                    if (ChartPictureTypeResetMethodInfo == null)
                        ChartPictureTypeResetMethodInfo = chartPictureValue.GetType().GetMethod("ResetMinMaxFromData", BindingFlags.Instance | BindingFlags.NonPublic);
                    ChartPictureTypeResetMethodInfo.Invoke(chartPictureValue, Array.Empty<object>());
                }
            }

            dataPointCollection.SuspendUpdates();
            for (int i = dataPointCollection.Count - 1; i >= 0; --i)
                dataPointCollection.RemoveAt(i);
            dataPointCollection.ResumeUpdates();
        }
    }
}