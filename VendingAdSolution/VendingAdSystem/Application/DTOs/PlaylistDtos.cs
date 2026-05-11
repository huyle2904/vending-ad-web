namespace VendingAdSystem.Application.DTOs;

public class PlaylistOrderUpdate
{
    public int PlaylistItemId { get; set; }
    public int OrderIndex { get; set; }
}

public class UpdatePlaylistRequest
{
    public int PlaylistId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class PlaylistActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
