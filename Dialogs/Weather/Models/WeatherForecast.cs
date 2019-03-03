using System;
using System.Collections.Generic;

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BasicBot.Dialogs.Weather.Models
{
    public partial class WeatherForecast
    {
        [JsonProperty("cod")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Cod { get; set; }

        [JsonProperty("message")]
        public double Message { get; set; }

        [JsonProperty("cnt")]
        public long Cnt { get; set; }

        [JsonProperty("list")]
        public List[] List { get; set; }

        [JsonProperty("city")]
        public City City { get; set; }
    }

    public partial class City
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("coord")]
        public Coord Coord { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("population")]
        public long Population { get; set; }
    }



    public partial class List
    {
        [JsonProperty("dt")]
        public long Dt { get; set; }

        [JsonProperty("main")]
        public MainClass Main { get; set; }

        [JsonProperty("weather")]
        public Weather[] Weather { get; set; }

        [JsonProperty("clouds")]
        public Clouds Clouds { get; set; }

        [JsonProperty("wind")]
        public Wind Wind { get; set; }

        [JsonProperty("snow")]
        public Rain Snow { get; set; }

        [JsonProperty("sys")]
        public Sys Sys { get; set; }

        [JsonProperty("dt_txt")]
        public DateTimeOffset DtTxt { get; set; }

        [JsonProperty("rain", NullValueHandling = NullValueHandling.Ignore)]
        public Rain Rain { get; set; }
    }

    //public partial class Clouds
    //{
    //    [JsonProperty("all")]
    //    public long All { get; set; }
    //}

    public partial class MainClass
    {
        [JsonProperty("temp")]
        public double Temp { get; set; }

        [JsonProperty("temp_min")]
        public double TempMin { get; set; }

        [JsonProperty("temp_max")]
        public double TempMax { get; set; }

        [JsonProperty("pressure")]
        public double Pressure { get; set; }

        [JsonProperty("sea_level")]
        public double SeaLevel { get; set; }

        [JsonProperty("grnd_level")]
        public double GrndLevel { get; set; }

        [JsonProperty("humidity")]
        public long Humidity { get; set; }

        [JsonProperty("temp_kf")]
        public double TempKf { get; set; }
    }

    public partial class Rain
    {
        [JsonProperty("3h", NullValueHandling = NullValueHandling.Ignore)]
        public double? The3H { get; set; }
    }

    public partial class Sys
    {
        [JsonProperty("pod")]
        public Pod Pod { get; set; }
    }

    //public partial class Weather
    //{
    //    [JsonProperty("id")]
    //    public long Id { get; set; }

    //    [JsonProperty("main")]
    //    public MainEnum Main { get; set; }

    //    [JsonProperty("description")]
    //    public Description Description { get; set; }

    //    [JsonProperty("icon")]
    //    public string Icon { get; set; }
    //}

    //public partial class Wind
    //{
    //    [JsonProperty("speed")]
    //    public double Speed { get; set; }

    //    [JsonProperty("deg")]
    //    public double Deg { get; set; }
    //}

    public enum Pod { D, N };

    public enum Description { BrokenClouds, ClearSky, FewClouds, LightRain, OvercastClouds, ScatteredClouds };

    public enum MainEnum { Clear, Clouds, Rain };

    public partial class WeatherForecast
    {
        public static WeatherForecast FromJson(string json) => JsonConvert.DeserializeObject<WeatherForecast>(json, Converter.Settings);
    }

    //public static class Serialize
    //{
    //    public static string ToJson(this WeatherForecast self) => JsonConvert.SerializeObject(self, Converter.Settings);
    //}

    //internal static class Converter
    //{
    //    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    //    {
    //        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
    //        DateParseHandling = DateParseHandling.None,
    //        Converters =
    //        {
    //            PodConverter.Singleton,
    //            DescriptionConverter.Singleton,
    //            MainEnumConverter.Singleton,
    //            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
    //        },
    //    };
    //}

    internal class ParseStringConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(long) || t == typeof(long?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            long l;
            if (Int64.TryParse(value, out l))
            {
                return l;
            }
            throw new Exception("Cannot unmarshal type long");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (long)untypedValue;
            serializer.Serialize(writer, value.ToString());
            return;
        }

        public static readonly ParseStringConverter Singleton = new ParseStringConverter();
    }

    internal class PodConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Pod) || t == typeof(Pod?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "d":
                    return Pod.D;
                case "n":
                    return Pod.N;
            }
            throw new Exception("Cannot unmarshal type Pod");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (Pod)untypedValue;
            switch (value)
            {
                case Pod.D:
                    serializer.Serialize(writer, "d");
                    return;
                case Pod.N:
                    serializer.Serialize(writer, "n");
                    return;
            }
            throw new Exception("Cannot marshal type Pod");
        }

        public static readonly PodConverter Singleton = new PodConverter();
    }

    internal class DescriptionConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Description) || t == typeof(Description?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "broken clouds":
                    return Description.BrokenClouds;
                case "clear sky":
                    return Description.ClearSky;
                case "few clouds":
                    return Description.FewClouds;
                case "light rain":
                    return Description.LightRain;
                case "overcast clouds":
                    return Description.OvercastClouds;
                case "scattered clouds":
                    return Description.ScatteredClouds;
            }
            throw new Exception("Cannot unmarshal type Description");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (Description)untypedValue;
            switch (value)
            {
                case Description.BrokenClouds:
                    serializer.Serialize(writer, "broken clouds");
                    return;
                case Description.ClearSky:
                    serializer.Serialize(writer, "clear sky");
                    return;
                case Description.FewClouds:
                    serializer.Serialize(writer, "few clouds");
                    return;
                case Description.LightRain:
                    serializer.Serialize(writer, "light rain");
                    return;
                case Description.OvercastClouds:
                    serializer.Serialize(writer, "overcast clouds");
                    return;
                case Description.ScatteredClouds:
                    serializer.Serialize(writer, "scattered clouds");
                    return;
            }
            throw new Exception("Cannot marshal type Description");
        }

        public static readonly DescriptionConverter Singleton = new DescriptionConverter();
    }

    internal class MainEnumConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(MainEnum) || t == typeof(MainEnum?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "Clear":
                    return MainEnum.Clear;
                case "Clouds":
                    return MainEnum.Clouds;
                case "Rain":
                    return MainEnum.Rain;
            }
            throw new Exception("Cannot unmarshal type MainEnum");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (MainEnum)untypedValue;
            switch (value)
            {
                case MainEnum.Clear:
                    serializer.Serialize(writer, "Clear");
                    return;
                case MainEnum.Clouds:
                    serializer.Serialize(writer, "Clouds");
                    return;
                case MainEnum.Rain:
                    serializer.Serialize(writer, "Rain");
                    return;
            }
            throw new Exception("Cannot marshal type MainEnum");
        }

        public static readonly MainEnumConverter Singleton = new MainEnumConverter();
    }
}