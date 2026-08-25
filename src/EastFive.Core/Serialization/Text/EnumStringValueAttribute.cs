using System;

namespace EastFive.Serialization.Text
{
	public class EnumStringValueAttribute : Attribute, IMapEnumValues
    {
        public string Value { get; set; }

        public StringComparison ComparisonMethod { get; set; } = StringComparison.OrdinalIgnoreCase;

        public EnumStringValueAttribute()
        {
        }

        public EnumStringValueAttribute(string value)
        {
        }

        public bool DoesMatch(string value)
        {
            return String.Equals(value, Value, ComparisonMethod);
        }
    }

    public class EnumStringValue2Attribute : EnumStringValueAttribute
    {
        public EnumStringValue2Attribute() : base() { }
        public EnumStringValue2Attribute(string value) : base(value) { }
    }
    public class EnumStringValue3Attribute : EnumStringValueAttribute
    {
        public EnumStringValue3Attribute() : base() { }
        public EnumStringValue3Attribute(string value) : base(value) { }
    }
    public class EnumStringValue4Attribute : EnumStringValueAttribute
    {
        public EnumStringValue4Attribute() : base() { }
        public EnumStringValue4Attribute(string value) : base(value) { }
    }
    public class EnumStringValue5Attribute : EnumStringValueAttribute
    {
        public EnumStringValue5Attribute() : base() { }
        public EnumStringValue5Attribute(string value) : base(value) { }
    }
    public class EnumStringValue6Attribute : EnumStringValueAttribute
    {
        public EnumStringValue6Attribute() : base() { }
        public EnumStringValue6Attribute(string value) : base(value) { }
    }
    public class EnumStringValue7Attribute : EnumStringValueAttribute
    {
        public EnumStringValue7Attribute() : base() { }
        public EnumStringValue7Attribute(string value) : base(value) { }
    }

    public class EnumWhitespaceValueAttribute : Attribute, IMapEnumValues
    {
        public string Value { get; set; }

        public bool DoesMatch(string value)
        {
            return value.IsNullOrWhiteSpace();
        }
    }
}
