using System.Data;

namespace libusf
{
    using PtType = ushort;

    public class CompactUsf
    {
        public class DataColumn
        {
            public double CastMinValue;
            public double CastMaxValue;

            public List<PtType?> DataRows = new();
        }

        public Dictionary<string, string> Header { get; set; } = new();

        public List<DataColumn> DataTable = new();

        public int RowNumber
        {
            get
            {
                if (DataTable.Count == 0)
                {
                    return 0;
                }
                else
                {
                    return DataTable[0].DataRows.Count;
                }
            }
        }

        public int ColumnNumber
        {
            get
            {
                return DataTable.Count;
            }
        }

        public static CompactUsf FromUsf(UsfFile data)
        {
            CompactUsf cusf = new();

            cusf.Header = data.Header;

            for (int i = 0; i < data.DataTable.ColumnNumber; ++i)
            {
                var col = data.DataTable.GetColumn(i);

                DataColumn column = new();

                var colmin = col.MinValue();
                var colmax = col.MaxValue();

                if (colmin == null || colmax == null)
                {
                    throw new Exception($"The minimal or the maximal value of column {i} is unavailable");
                }

                column.CastMinValue = colmin.Value;
                column.CastMaxValue = colmax.Value;

                column.DataRows = Enumerable.Repeat<PtType?>(null, col.RowNumber).ToList();

                for (int j = 0; j < col.RowNumber; ++j)
                {
                    if (col[0, j] == null)
                    {
                        column.DataRows[j] = PtType.MaxValue;
                    }
                    else
                    {
                        column.DataRows[j] = (PtType?)((col[0, j] - colmin) / (colmax - colmin) * (PtType.MaxValue - 1));
                    }
                }

                cusf.DataTable.Add(column);
            }

            return cusf;
        }

        public static CompactUsf FromStream(BinaryReader br)
        {
            CompactUsf cusf = new();

            byte[] magic = new byte[4];
            br.Read(magic, 0, 4);

            if (!(magic[0] == 67 && magic[1] == 85 && magic[2] == 83 && magic[3] == 70))
            {
                throw new Exception("cusf Error: invalid magic.");
            }

            int row = br.ReadInt32();
            int col = br.ReadInt32();

            if (row <= 0 || col <= 0)
            {
                throw new Exception("cusf Error: invalid row/col number.");
            }

            for (int i = 0; i < col; ++i)
            {
                DataColumn column = new();

                column.CastMinValue = br.ReadDouble();
                column.CastMaxValue = br.ReadDouble();

                for (int j = 0; j < row; ++j)
                {
                    PtType val = br.ReadUInt16();

                    column.DataRows.Add(val == PtType.MaxValue ? null : val);
                }

                cusf.DataTable.Add(column);
            }

            magic = new byte[4];
            br.Read(magic, 0, 4);

            if (!(magic[0] == 70 && magic[1] == 83 && magic[2] == 85 && magic[3] == 67))
            {
                throw new Exception("cusf Error: invalid magic.");
            }

            return cusf;
        }

        public void ToStream(BinaryWriter bw)
        {
            bw.Write(new byte[] { 67, 85, 83, 70 }); // CUSF: File header magic

            bw.Write(RowNumber);
            bw.Write(ColumnNumber);

            for (int i = 0; i < ColumnNumber; ++i)
            {
                bw.Write(DataTable[i].CastMinValue);
                bw.Write(DataTable[i].CastMaxValue);

                for (int j = 0; j < RowNumber; ++j)
                {
                    if (DataTable[i].DataRows[j] == null)
                    {
                        bw.Write(PtType.MaxValue);
                    }
                    else
                    {
                        bw.Write(DataTable[i].DataRows[j] ?? PtType.MaxValue);
                    }
                }
            }

            bw.Write(new byte[] { 70, 83, 85, 67 }); // FSUC: File tail magic
        }

        public UsfFile ToUsf()
        {
            UsfFile usf = new();

            usf.DataTable.InitializeShape(ColumnNumber, RowNumber);
            for (int i = 0; i < ColumnNumber; ++i)
            {
                for (int j = 0; j < RowNumber; ++j)
                {
                    usf.DataTable[i, j] = this[i, j];
                }
            }

            return usf;
        }

        // Using multiple times of writing is not recommended due to the precision cutoff.
        public double? this[int col, int row]
        {
            get
            {
                return DataTable[col].DataRows[row] *
                    (DataTable[col].CastMaxValue - DataTable[col].CastMinValue) / (PtType.MaxValue - 1) +
                    DataTable[col].CastMinValue;
            }

            set
            {
                if (value == null)
                {
                    DataTable[col].DataRows[row] = null;
                }
                else
                {
                    if (value > DataTable[col].CastMaxValue)
                    {
                        DataTable[col].DataRows = DataTable[col].DataRows.Select(x =>
                            x == null ? null :
                            (PtType?)(x * (DataTable[col].CastMaxValue - DataTable[col].CastMinValue) / (value - DataTable[col].CastMinValue))
                        ).ToList();
                    }

                    if (value < DataTable[col].CastMinValue)
                    {
                        DataTable[col].DataRows = DataTable[col].DataRows.Select(x =>
                            x == null ? null :
                            (PtType?)((x * (DataTable[col].CastMaxValue - DataTable[col].CastMinValue) + DataTable[col].CastMinValue - value) / (DataTable[col].CastMaxValue - value))
                        ).ToList();
                    }

                    DataTable[col].DataRows[row] = (PtType)((value - DataTable[col].CastMinValue) /
                        (DataTable[col].CastMaxValue - DataTable[col].CastMinValue) * (PtType.MaxValue - 1));
                }
            }
        }
    }
}
