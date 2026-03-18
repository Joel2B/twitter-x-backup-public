namespace Backup.App.Models.Config;

public class Dump : Downloads.Path
{
    public required int Count { get; set; }
}

// public class Data : Downloads.Path
// {
//     public required Dumps Dumps { get; set; }
// }

// public class Dumps : Downloads.Path
// {
//     public required Dump Dump { get; set; }
// }

// public class Dump : Downloads.Path
// {
//     public required int Count { get; set; }
//     public required Downloads.Path Api { get; set; }
// }
