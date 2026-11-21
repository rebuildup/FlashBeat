using System.Runtime.CompilerServices;

// EditMode テスト (Assets/Tests/Editor, Assets/Game/Tests) が
// V6SongBadge.ApplyRemoteJacket 等の internal な適用経路を直接検証できるようにする
// (NoteEditor/Scripts/AssemblyInfo.cs と同じ運用)。
[assembly: InternalsVisibleTo("FlashBeat.Tests")]
[assembly: InternalsVisibleTo("FlashBeat.V6CanvasTests")]
