using System;

namespace EastFive.Serialization.Parquet
{
    /// <summary>
    /// One cell of a parquet file that could not be converted into its member. The row it belongs to
    /// is left out of the parsed rows; a row with several unconvertible cells appears once per cell.
    /// </summary>
    /// <param name="Row">Zero-based index of the row across every row group of the file.</param>
    /// <param name="Column">Name of the file column that held the value.</param>
    /// <param name="RawValue">The value as read from the file.</param>
    /// <param name="Reason">Why the value could not be converted.</param>
    public sealed record UnparsedRow(int Row, string Column, object RawValue, string Reason);
}
