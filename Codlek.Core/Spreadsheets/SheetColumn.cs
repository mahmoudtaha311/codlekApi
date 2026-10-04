namespace Codlek.Core.Spreadsheets;

/// <summary>عمود في صفحة إكسل: عنوان وعرض.</summary>
public readonly record struct SheetColumn(string Header, double Width = 16);
