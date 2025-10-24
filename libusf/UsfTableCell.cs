namespace libusf
{
    public sealed class UsfTableCell
    {
        public UsfTableCell()
        {
            Value = null;
        }

        public UsfTableCell(double value)
        {
            Value = value;
        }

        static public UsfTableCell Parse(string value)
        {
            value = value.Trim();

            if (value == "?")
            {
                return new UsfTableCell();
            }
            else
            {
                return new UsfTableCell(double.Parse(value));
            }
        }

        public bool ValuePresent
        {
            get
            {
                return Value != null;
            }
        }

        public override string ToString()
        {
            if (ValuePresent)
            {
                return Value.ToString() ?? "?";
            }
            else
            {
                return "?";
            }
        }

        public double? Value { get; set; }
    }
}
