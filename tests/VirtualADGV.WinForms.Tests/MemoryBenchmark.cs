using System.Diagnostics;

internal static class MemoryBenchmark
{
    [STAThread]
    internal static int Run(string[] args)
    {
        Application.EnableVisualStyles();
        using var form = new Form { Width = 1000, Height = 650 };
        using var grid = new DataGridView { Dock = DockStyle.Fill, VirtualMode = true, AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None };
        grid.Columns.Add("Ordinal", "Ordinal");
        form.Controls.Add(grid);
        form.Show();
        Application.DoEvents();
        int count = args.Length > 1 ? int.Parse(args[1]) : 10_000_000;
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long managed = GC.GetTotalMemory(true);
        long privateBytes = Process.GetCurrentProcess().PrivateMemorySize64;
        var timer = Stopwatch.StartNew();
        grid.RowCount = count;
        timer.Stop();
        Application.DoEvents();
        long after = GC.GetTotalMemory(true);
        Console.WriteLine($"RowCount={grid.RowCount} managed_delta={after - managed} private_delta={Process.GetCurrentProcess().PrivateMemorySize64 - privateBytes} setup_ms={timer.Elapsed.TotalMilliseconds:F3}");
        return grid.RowCount == count ? 0 : 1;
    }
}
