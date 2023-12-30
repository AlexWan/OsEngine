using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing; // Для отрисовки
using OsEngine.Entity; // Базовые элементы 
using OsEngine.Indicators; // Индикаторыне элементы


namespace OsEngine.Indicators.LearnOsE // Название вашего просттранства 
{
    [IndicatorAttribute("LinesSupportResistant")] // Название индикатора. Чтобы система добавила его автоматом в списко доступных

    internal class LinesSupportResistant : Aindicator // Наследуем от базового класса индикатор
    {
        private IndicatorParameterInt _lenghtHistory; // Параметры индикатора
 
        private IndicatorDataSeries _seriesUp, _seriesDown; // Переменные индикатора

        public override void OnStateChange(IndicatorState state) // Иницатлизация индикатора
        {
            if (state == IndicatorState.Configure) // событие настройки
            {
                _lenghtHistory = CreateParameterInt("History length", 60); // Создание параметра
                _seriesUp = CreateSeries("Resistant line", Color.Red, IndicatorChartPaintType.Line, true); // Инициализация серии
                _seriesDown = CreateSeries("Support line", Color.GreenYellow, IndicatorChartPaintType.Line, true); // Инициализация серии

            }
            else if (state == IndicatorState.Dispose) //Событие закрытие индикатора
            {
                // Clear temp data
            }
        }
        public override void OnProcess(List<Candle> candels, int index) // Событие получение новой свечки. Логика индикатора
        {
            decimal upLine = 0, downLine = 0;
            if (index - _lenghtHistory.ValueInt > 0)
            {
                upLine = candels[index].High; 
                downLine = candels[index].Low; 
 

                for (int i = index; i > 3 && i > index - _lenghtHistory.ValueInt; i--)
                {
                    if (upLine < candels[i-1].High)
                    {
                        upLine = candels[i - 1].High;
                    }
                    else if(upLine> candels[index].High)
                    {
                        break;
                    }

                }
                for (int i = index; i > 3 && i > index - _lenghtHistory.ValueInt; i--)
                {
                    if (downLine > candels[i-1].Low)
                    {
                        downLine = candels[i - 1].Low;
                    }
                    else if(downLine< candels[index].Low)
                    {
                        break;
                    }

                }
                _seriesUp.Values[index ] = upLine;
                _seriesDown.Values[index] = downLine;
            }
        }

    }
}
