//    using OsEngine.Market.Servers.Bitfinex.BitfitnexEntity;
//
//    var trades = BitfinexSnapshotParser.FromJson(jsonString);

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OsEngine.Market.Servers.Bitfinex.BitfitnexEntity
{

    #region Парсер тиков

    public partial struct ChangedElement
    {
        public double? Double;
        public string String;

        public bool IsNull
        {
            get { return Double == null && String == null; }
        }
    }

    public class UpdateDataBitfinex
    {
        public static List<ChangedElement> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<ChangedElement>>(json,
                ConverterTicks.Settings);
        }
    }

    internal static class ConverterTicks
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                TickElementConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    internal class TickElementConverter : JsonConverter
    {
        public override bool CanConvert(Type t)
        {
            return t == typeof (ChangedElement) || t == typeof (ChangedElement?);
        }

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            try
            {
                switch (reader.TokenType)
                {
                    case JsonToken.Integer:
                    case JsonToken.Float:
                        var doubleValue = serializer.Deserialize<double>(reader);
                        return new ChangedElement { Double = doubleValue };

                    case JsonToken.String:
                    case JsonToken.Date:
                        var stringValue = serializer.Deserialize<string>(reader);
                        return new ChangedElement { String = stringValue };

                    default:
                        return null;                            
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (ChangedElement)untypedValue;
            if (value.Double != null)
            {
                serializer.Serialize(writer, value.Double.Value);
                return;
            }
            if (value.String != null)
            {
                serializer.Serialize(writer, value.String);
                return;
            }
            throw new Exception("Cannot marshal type ChangedElement");
        }

        public static readonly TickElementConverter Singleton = new TickElementConverter();
    }

    #endregion

    #region Парсер снимков

    public partial struct DataObject
    {
        public List<List<double>> Values;
        public long? IdChanel;

        public bool IsNull
        {
            get { return Values == null && IdChanel == null; }
        }
    }

    public class BitfinexSnapshotParser
    {
        public static List<DataObject> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<DataObject>>(json,
                Converter.Settings);
        }
    }

    //public static class Serialize
    //{
    //    public static string ToJson(this List<DataObject> self) => JsonConvert.SerializeObject(self, OsEngine.Market.Servers.Bitfinex.BitfitnexEntity.Converter.Settings);
    //}

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                TradeConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    internal class TradeConverter : JsonConverter
    {
        public override bool CanConvert(Type t)
        {
            return t == typeof (DataObject) || t == typeof (DataObject?);
        }

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    var integerValue = serializer.Deserialize<long>(reader);
                    return new DataObject { IdChanel = integerValue };
                case JsonToken.StartArray:
                    var arrayValue = serializer.Deserialize<List<List<double>>>(reader);
                    return new DataObject { Values = arrayValue };
            }
            throw new Exception("Cannot unmarshal type DataObject");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (DataObject)untypedValue;
            if (value.IdChanel != null)
            {
                serializer.Serialize(writer, value.IdChanel.Value);
                return;
            }
            if (value.Values != null)
            {
                serializer.Serialize(writer, value.Values);
                return;
            }
            throw new Exception("Cannot marshal type DataObject");
        }

        public static readonly TradeConverter Singleton = new TradeConverter();
    }

    #endregion

    #region Парсер свечей

    public class Candles
    {
        public static List<List<double>> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<List<double>>>(json,
                CandlesConverter.Settings);
        }
    }

    //public static class Serialize
    //{
    //    public static string ToJson(this List<List<double>> self) => JsonConvert.SerializeObject(self, OsEngine.Market.Servers.Bitfinex.BitfitnexEntity.CandlesConverter.Settings);
    //}

    internal static class CandlesConverter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    public class LastCandle
    {
        public static List<double> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<double>>(json,
                ConverterLastCandle.Settings);
        }
    }

    internal static class ConverterLastCandle
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    #endregion

    #region parser of portfolio snapshot / Парсер снимка портфелей

    public partial struct WaletWalet
    {
        public double? Double;
        public string String;

        public bool IsNull
        {
            get { return Double == null && String == null; }
        }
    }

    public partial struct PurpleWalet
    {
        public List<List<WaletWalet>> AllWallets;
        public long? IdChannel;
        public string MessageName;

        public bool IsNull
        {
            get { return AllWallets == null && IdChannel == null && MessageName == null; }
        }
    }

    public class Walets
    {
        public static List<PurpleWalet> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<PurpleWalet>>(json, ConverterWallets.Settings);
        }
    }

    internal static class ConverterWallets
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                PurpleWaletConverter.Singleton,
                WaletWaletConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    internal class PurpleWaletConverter : JsonConverter
    {
        public override bool CanConvert(Type t)
        {
            return t == typeof (PurpleWalet) || t == typeof (PurpleWalet?);
        }

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    var integerValue = serializer.Deserialize<long>(reader);
                    return new PurpleWalet { IdChannel = integerValue };
                case JsonToken.String:
                case JsonToken.Date:
                    var stringValue = serializer.Deserialize<string>(reader);
                    return new PurpleWalet { MessageName = stringValue };
                case JsonToken.StartArray:
                    var arrayValue = serializer.Deserialize<List<List<WaletWalet>>>(reader);
                    return new PurpleWalet { AllWallets = arrayValue };
            }
            throw new Exception("Cannot unmarshal type PurpleWalet");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (PurpleWalet)untypedValue;
            if (value.IdChannel != null)
            {
                serializer.Serialize(writer, value.IdChannel.Value);
                return;
            }
            if (value.MessageName != null)
            {
                serializer.Serialize(writer, value.MessageName);
                return;
            }
            if (value.AllWallets != null)
            {
                serializer.Serialize(writer, value.AllWallets);
                return;
            }
            throw new Exception("Cannot marshal type PurpleWalet");
        }

        public static readonly PurpleWaletConverter Singleton = new PurpleWaletConverter();
    }

    internal class WaletWaletConverter : JsonConverter
    {
        public override bool CanConvert(Type t)
        {
            return t == typeof (WaletWalet) || t == typeof (WaletWalet?);
        }

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                case JsonToken.Float:
                    var doubleValue = serializer.Deserialize<double>(reader);
                    return new WaletWalet { Double = doubleValue };
                case JsonToken.String:
                case JsonToken.Date:
                    var stringValue = serializer.Deserialize<string>(reader);
                    return new WaletWalet { String = stringValue };
            }
            throw new Exception("Cannot unmarshal type WaletWalet");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (WaletWalet)untypedValue;
            if (value.Double != null)
            {
                serializer.Serialize(writer, value.Double.Value);
                return;
            }
            if (value.String != null)
            {
                serializer.Serialize(writer, value.String);
                return;
            }
            throw new Exception("Cannot marshal type WaletWalet");
        }

        public static readonly WaletWaletConverter Singleton = new WaletWaletConverter();
    }

    #endregion

    #region portfolio updated / Обновился портфель

    public partial struct WalletUpdateWalletUpdate
    {
        public double? Double;
        public string String;

        public bool IsNull
        {
            get { return Double == null && String == null; }
        }
    }

    public partial struct PurpleWalletUpdate
    {
        public List<WalletUpdateWalletUpdate> UpdatedWallet;
        public long? Integer;
        public string String;

        public bool IsNull
        {
            get { return UpdatedWallet == null && Integer == null && String == null; }
        }
    }

    public class WalletUpdate
    {
        public static List<PurpleWalletUpdate> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<PurpleWalletUpdate>>(json, ConverterWallet.Settings);
        }
    }

    internal static class ConverterWallet
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = {
                PurpleWalletUpdateConverter.Singleton,
                WalletUpdateWalletUpdateConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    internal class PurpleWalletUpdateConverter : JsonConverter
    {
        public override bool CanConvert(Type t)
        {
            return t == typeof (PurpleWalletUpdate) || t == typeof (PurpleWalletUpdate?);
        }

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    var integerValue = serializer.Deserialize<long>(reader);
                    return new PurpleWalletUpdate { Integer = integerValue };
                case JsonToken.String:
                case JsonToken.Date:
                    var stringValue = serializer.Deserialize<string>(reader);
                    return new PurpleWalletUpdate { String = stringValue };
                case JsonToken.StartArray:
                    var arrayValue = serializer.Deserialize<List<WalletUpdateWalletUpdate>>(reader);
                    return new PurpleWalletUpdate { UpdatedWallet = arrayValue };
            }
            throw new Exception("Cannot unmarshal type PurpleWalletUpdate");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (PurpleWalletUpdate)untypedValue;
            if (value.Integer != null)
            {
                serializer.Serialize(writer, value.Integer.Value);
                return;
            }
            if (value.String != null)
            {
                serializer.Serialize(writer, value.String);
                return;
            }
            if (value.UpdatedWallet != null)
            {
                serializer.Serialize(writer, value.UpdatedWallet);
                return;
            }
            throw new Exception("Cannot marshal type PurpleWalletUpdate");
        }

        public static readonly PurpleWalletUpdateConverter Singleton = new PurpleWalletUpdateConverter();
    }

    internal class WalletUpdateWalletUpdateConverter : JsonConverter
    {
        public override bool CanConvert(Type t)
        {
            return t == typeof (WalletUpdateWalletUpdate) || t == typeof (WalletUpdateWalletUpdate?);
        }

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                case JsonToken.Float:
                    var doubleValue = serializer.Deserialize<double>(reader);
                    return new WalletUpdateWalletUpdate { Double = doubleValue };
                case JsonToken.String:
                case JsonToken.Date:
                    var stringValue = serializer.Deserialize<string>(reader);
                    return new WalletUpdateWalletUpdate { String = stringValue };
            }
            throw new Exception("Cannot unmarshal type WalletUpdateWalletUpdate");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (WalletUpdateWalletUpdate)untypedValue;
            if (value.Double != null)
            {
                serializer.Serialize(writer, value.Double.Value);
                return;
            }
            if (value.String != null)
            {
                serializer.Serialize(writer, value.String);
                return;
            }
            throw new Exception("Cannot marshal type WalletUpdateWalletUpdate");
        }

        public static readonly WalletUpdateWalletUpdateConverter Singleton = new WalletUpdateWalletUpdateConverter();
    }

    #endregion
}
