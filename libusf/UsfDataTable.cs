using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace libusf
{
    public class UsfDataTable
    {
        public UsfDataTable()
        {
            Table = new();
        }

        public UsfDataTable(int NumberOfColumns, int NumberOfRows)
        {
            Table = new();
            InitializeShape(NumberOfColumns, NumberOfRows);
        }

        public int RowNumber
        {
            get
            {
                return Table.Count;
            }
        }

        public int ColumnNumber
        {
            get
            {
                if (Table.Count == 0)
                {
                    return 0;
                }

                return Table[0].Count;
            }
        }

        public double? this[int col, int row]
        {
            get
            {
                return Table[row][col].Value;
            }
            set
            {
                Table[row][col].Value = value;
            }
        }

        public void SetColumn(int index, IEnumerable<UsfTableCell> arr)
        {
            SetColumn(index, arr.Select(x => x.Value));
        }

        public void SetColumn(int index, IEnumerable<double?> arr)
        {
            if (arr.Count() != RowNumber)
            {
                throw new Exception("Column length mismatch.");
            }

            int i = 0;
            foreach (var val in arr)
            {
                Table[i][index].Value = val;
                ++i;
            }
        }

        public void SetRow(int index, IEnumerable<UsfTableCell> arr)
        {
            SetRow(index, arr.Select(x => x.Value));
        }

        public void SetRow(int index, IEnumerable<double?> arr)
        {
            if (arr.Count() != ColumnNumber)
            {
                throw new Exception("Row length mismatch.");
            }

            int i = 0;
            foreach (var val in arr)
            {
                Table[index][i].Value = val;
                ++i;
            }
        }

        static public UsfDataTable ParseUsfTableString(string UsfTableString)
        {
            var lines = UsfTableString.Split('\n').Select(x => x.ToUpper().Trim()).ToList();

            int nCol, nRow;

            var TableHeader = lines[0].Split(" ").Select(x => x.Trim()).ToList();
            if (TableHeader[0] != "H")
            {
                throw new Exception("Exprected H on header line.");
            }

            nCol = int.Parse(TableHeader[1]);
            nRow = int.Parse(TableHeader[2]);

            UsfDataTable newUsf = new(nCol, nRow);

            lines = lines.Skip(1).ToList();

            for (int i = 0; i < nRow; i++)
            {
                var lineItems = lines[i].Split(" ").Select(x => x.Trim()).ToList();

                if (lineItems[0] != "T")
                {
                    throw new Exception("Exprected T on table line.");
                }

                lineItems = lineItems.Skip(1).ToList();

                for (int j = 0; j < nCol; j++)
                {
                    newUsf.Table[i][j] = UsfTableCell.Parse(lineItems[j]);
                }
            }

            return newUsf;
        }

        public void InitializeShape(int NumberOfColumns, int NumberOfRows)
        {
            for (int i = 0; i < NumberOfRows; ++i)
            {
                Table.Add(new());
                for (int j = 0; j < NumberOfColumns; ++j)
                {
                    Table[i].Add(new UsfTableCell());
                }
            }
        }

        public UsfDataTable GetRow(int RowIndex)
        {
            var row = GetRowEnumerable(RowIndex);
            var rowUsf = new UsfDataTable();

            rowUsf.Table.Add(row.ToList());
            return rowUsf;
        }

        public IEnumerable<UsfTableCell> GetRowEnumerable(int RowIndex)
        {
            return Table[RowIndex];
        }

        public UsfDataTable GetColumn(int ColumnIndex)
        {
            var col = GetColumnEnumerable(ColumnIndex);
            var colUsf = new UsfDataTable();

            colUsf.Table = col.Select(x => new List<UsfTableCell> { x }).ToList();
            return colUsf;
        }

        public IEnumerable<UsfTableCell> GetColumnEnumerable(int ColumnIndex)
        {
            return Table.Select(x => x[ColumnIndex]).ToList();
        }

        public Dictionary<double, double?> GetColumnPair(int keyColumnIndex, int valueColumnIndex)
        {
            Dictionary<double, double?> dict = new();

            var colKey = GetColumnEnumerable(keyColumnIndex).ToList();
            var colValue = GetColumnEnumerable(valueColumnIndex).ToList();

            if (colKey.Count() != colValue.Count())
            {
                throw new Exception("Mismatch column length");
            }

            for (int i = 0; i < colKey.Count(); i++)
            {
                var key = colKey[i].Value;

                if (key != null && colKey[i].ValuePresent)
                {
                    dict.Add(key.Value, colValue[i].Value);
                }
                else
                {
                    throw new Exception("Missing key cell value.");
                }
            }

            return dict;
        }

        public UsfDataTable Transverse()
        {
            var TransversedUsf = new UsfDataTable(RowNumber, ColumnNumber);

            for (int i = 0; i < RowNumber; ++i)
                for (int j = 0; j < ColumnNumber; ++j)
                    TransversedUsf.Table[j][i] = Table[i][j];

            return TransversedUsf;
        }

        public override string ToString()
        {
            return "[" + string.Join(",\n", Table.Select(x => "[" + string.Join(", ", x.Select(y => y.ToString()).ToList()) + "]")) + "]";
        }

        public string ToUsfTableString()
        {
            return $"H {ColumnNumber} {RowNumber}\n" +
                string.Join("\n", Table.Select(x => "T " + string.Join(" ", x.Select(y => y.ToString()).ToList())));
        }

        public double? MaxValue()
        {
            if (ColumnNumber == 0 || RowNumber == 0)
            {
                return null;
            }

            double? value = Table[0][0].Value;
            foreach (var row in Table)
            {
                foreach (var cell in row)
                {
                    if (cell.Value != null)
                    {
                        if (value == null)
                        {
                            value = cell.Value;
                        }
                        else
                        {
                            if (value < cell.Value)
                            {
                                value = cell.Value;
                            }
                        }
                    }
                }
            }

            return value;
        }

        public double? MinValue()
        {
            if (ColumnNumber == 0 || RowNumber == 0)
            {
                return null;
            }

            double? value = Table[0][0].Value;
            foreach (var row in Table)
            {
                foreach (var cell in row)
                {
                    if (cell.Value != null)
                    {
                        if (value == null)
                        {
                            value = cell.Value;
                        }
                        else
                        {
                            if (value > cell.Value)
                            {
                                value = cell.Value;
                            }
                        }
                    }
                }
            }

            return value;
        }

        public List<List<UsfTableCell>> Table;
    }
}
