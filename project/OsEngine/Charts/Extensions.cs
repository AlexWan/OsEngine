using System;
using System.Reflection;
using System.Windows.Forms.DataVisualization.Charting;

namespace OsEngine.Charts
{
    public static class Extensions
    {
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
            {
                return;
            }
                
            if(dataPointCollection.Count == 0)
            {
                return;
            }

            dataPointCollection.SuspendUpdates();

            for (int i = dataPointCollection.Count - 1; i >= 0; --i)
            {
                dataPointCollection.RemoveAt(i);
            }

            dataPointCollection.ResumeUpdates();
        }
    }
}