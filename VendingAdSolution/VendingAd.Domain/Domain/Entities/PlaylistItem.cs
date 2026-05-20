namespace VendingAdSystem.Domain.Entities;

public class PlaylistItem
{
    public int Id { get; set; }
    public int PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;
    public int MediaId { get; set; }
    public Media Media { get; set; } = null!;
    public int OrderIndex { get; set; }
}
