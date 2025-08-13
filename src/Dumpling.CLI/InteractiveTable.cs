using Spectre.Console;
using System.Text;

namespace Dumpling.CLI;

public class InteractiveTable<T>(string title, List<T> items)
{
    private readonly List<T> items = items;
    private readonly List<Column<T>> columns = new();
    private int selectedIndex;
    private int pageSize = 20;
    private int currentPage;
    private string sortColumn = "";
    private bool sortAscending;
    private string searchTerm = "";
    private List<T> filteredItems = items;

    public InteractiveTable<T> AddColumn(string name, Func<T, string> getValue, string? sortKey = null, Justify justify = Justify.Left)
    {
        columns.Add(new Column<T>
        {
            Name = name,
            GetValue = getValue,
            SortKey = sortKey ?? name,
            Justify = justify
        });
        return this;
    }
    
    public T? Show()
    {
        while (true)
        {
            RenderTable();
            var key = Console.ReadKey(true);
            
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (selectedIndex > 0)
                    {
                        selectedIndex--;
                        UpdatePage();
                    }
                    break;
                    
                case ConsoleKey.DownArrow:
                    if (selectedIndex < filteredItems.Count - 1)
                    {
                        selectedIndex++;
                        UpdatePage();
                    }
                    break;
                    
                case ConsoleKey.PageUp:
                    selectedIndex = Math.Max(0, selectedIndex - pageSize);
                    UpdatePage();
                    break;
                    
                case ConsoleKey.PageDown:
                    selectedIndex = Math.Min(filteredItems.Count - 1, selectedIndex + pageSize);
                    UpdatePage();
                    break;
                    
                case ConsoleKey.Home:
                    selectedIndex = 0;
                    currentPage = 0;
                    break;
                    
                case ConsoleKey.End:
                    selectedIndex = filteredItems.Count - 1;
                    UpdatePage();
                    break;
                    
                case ConsoleKey.Enter:
                    if (filteredItems.Count > 0 && selectedIndex < filteredItems.Count)
                    {
                        return filteredItems[selectedIndex];
                    }
                    break;
                    
                case ConsoleKey.Escape:
                case ConsoleKey.Q:
                    return default(T);
                    
                case ConsoleKey.D1:
                case ConsoleKey.D2:
                case ConsoleKey.D3:
                case ConsoleKey.D4:
                case ConsoleKey.D5:
                case ConsoleKey.D6:
                case ConsoleKey.D7:
                case ConsoleKey.D8:
                case ConsoleKey.D9:
                    int colIndex = (int)key.Key - (int)ConsoleKey.D1;
                    if (colIndex < columns.Count)
                    {
                        SortByColumn(colIndex);
                    }
                    break;
                    
                case ConsoleKey.F:
                    if (key.Modifiers == ConsoleModifiers.Control)
                    {
                        SearchFilter();
                    }
                    break;
                    
                case ConsoleKey.C:
                    if (key.Modifiers == ConsoleModifiers.Control)
                    {
                        ClearFilter();
                    }
                    break;
            }
        }
    }
    
    private void RenderTable()
    {
        AnsiConsole.Clear();
        
        // Title
        AnsiConsole.MarkupLine($"[bold yellow]{Markup.Escape(title)}[/]");
        AnsiConsole.WriteLine();
        
        // Create the table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.ShowRowSeparators = false;
        table.Expand = false;
        
        // Add columns with sort indicators
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var header = col.Name;
            
            // Add column number for sorting
            header = $"[dim]{i + 1}[/] {header}";
            
            // Add sort indicator
            if (sortColumn == col.SortKey)
            {
                header += sortAscending ? " [cyan]▲[/]" : " [cyan]▼[/]";
            }
            
            var tableColumn = new TableColumn(header);
            if (col.Justify == Justify.Right)
                tableColumn.RightAligned();
            else if (col.Justify == Justify.Center)
                tableColumn.Centered();
                
            table.AddColumn(tableColumn);
        }
        
        // Add rows
        int startIndex = currentPage * pageSize;
        int endIndex = Math.Min(startIndex + pageSize, filteredItems.Count);
        
        for (int i = startIndex; i < endIndex; i++)
        {
            var item = filteredItems[i];
            var values = columns.Select(c => c.GetValue(item)).ToArray();
            
            // Highlight selected row
            if (i == selectedIndex)
            {
                for (int j = 0; j < values.Length; j++)
                {
                    values[j] = $"[black on yellow]{values[j]}[/]";
                }
            }
            
            table.AddRow(values);
        }
        
        AnsiConsole.Write(table);
        
        // Status bar
        AnsiConsole.WriteLine();
        var statusText = new StringBuilder();
        statusText.Append($"[dim]Item {selectedIndex + 1} of {filteredItems.Count}");
        statusText.Append($" | Page {currentPage + 1} of {(filteredItems.Count - 1) / pageSize + 1}");
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            statusText.Append($" | Filter: {Markup.Escape(searchTerm)}");
        }
        
        if (!string.IsNullOrEmpty(sortColumn))
        {
            statusText.Append($" | Sort: {sortColumn} {(sortAscending ? "▲" : "▼")}");
        }
        
        statusText.Append("[/]");
        AnsiConsole.MarkupLine(statusText.ToString());
        
        // Help text
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Keys: [yellow]↑↓[/] Navigate | [yellow]Enter[/] Select | [yellow]1-9[/] Sort Column | [yellow]Ctrl+F[/] Filter | [yellow]Ctrl+C[/] Clear Filter | [yellow]Esc/Q[/] Back[/]");
    }
    
    private void UpdatePage()
    {
        currentPage = selectedIndex / pageSize;
    }
    
    private void SortByColumn(int columnIndex)
    {
        if (columnIndex >= columns.Count)
            return;
            
        var column = columns[columnIndex];
        
        // Toggle sort direction if same column
        if (sortColumn == column.SortKey)
        {
            sortAscending = !sortAscending;
        }
        else
        {
            sortColumn = column.SortKey;
            sortAscending = false;
        }
        
        ApplySort();
        selectedIndex = 0;
        currentPage = 0;
    }
    
    private void ApplySort()
    {
        if (string.IsNullOrEmpty(sortColumn))
        {
            filteredItems = ApplyFilter(items);
            return;
        }
        
        var column = columns.FirstOrDefault(c => c.SortKey == sortColumn);
        if (column == null)
        {
            filteredItems = ApplyFilter(items);
            return;
        }
        
        var sorted = sortAscending 
            ? filteredItems.OrderBy(item => column.GetValue(item))
            : filteredItems.OrderByDescending(item => column.GetValue(item));
            
        filteredItems = sorted.ToList();
    }
    
    private void SearchFilter()
    {
        AnsiConsole.WriteLine();
        var newSearch = AnsiConsole.Ask<string>("Enter search term (empty to clear):");
        searchTerm = newSearch;
        filteredItems = ApplyFilter(items);
        ApplySort();
        selectedIndex = 0;
        currentPage = 0;
    }
    
    private void ClearFilter()
    {
        searchTerm = "";
        filteredItems = ApplyFilter(items);
        ApplySort();
        selectedIndex = 0;
        currentPage = 0;
    }
    
    private List<T> ApplyFilter(List<T> source)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return source.ToList();
            
        return source.Where(item =>
        {
            return columns.Any(col => 
                col.GetValue(item).Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }).ToList();
    }
    
    private class Column<TItem>
    {
        public string Name { get; set; } = "";
        public Func<TItem, string> GetValue { get; set; } = _ => "";
        public string SortKey { get; set; } = "";
        public Justify Justify { get; set; } = Justify.Left;
    }
}

public enum Justify
{
    Left,
    Center,
    Right
}