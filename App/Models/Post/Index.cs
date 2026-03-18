namespace Backup.App.Models.Post;

public class IndexData
{
    public string? Previous { get; set; } = null;
    public string? Next { get; set; } = null;

    public override bool Equals(object? obj)
    {
        if (obj is not IndexData index)
            return false;

        return Previous == index.Previous && Next == index.Next;
    }

    public IndexData Clone() => new() { Previous = Previous, Next = Next };

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
