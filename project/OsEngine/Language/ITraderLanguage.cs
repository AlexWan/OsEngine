using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Language
{
    public interface ITraderLanguage
    {
        string UiTitle { get; }

        string TextButtonBuy { get; }

        string TextButtonSell { get; }

    }

    public enum Localization
    {
        rus,

        eng,
    }
}
