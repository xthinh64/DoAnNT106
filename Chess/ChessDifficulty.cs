namespace OnlineChessGame.Chess;

public sealed record ChessDifficulty(
    string Id,
    string DisplayName,
    string Description,
    int SkillLevel,
    int MoveTimeMilliseconds,
    int DisplayElo)
{
    public static ChessDifficulty Easy { get; } =
        new("easy", "Dễ", "Phù hợp người mới, suy nghĩ khoảng 150 ms.", 2, 150, 900);

    public static ChessDifficulty Medium { get; } =
        new("medium", "Trung bình", "Biết chiến thuật cơ bản, suy nghĩ khoảng 500 ms.", 7, 500, 1400);

    public static ChessDifficulty Hard { get; } =
        new("hard", "Khó", "Chơi chắc và nhìn sâu, suy nghĩ khoảng 1 giây.", 14, 1000, 2000);

    public static ChessDifficulty Expert { get; } =
        new("expert", "Chuyên gia", "Stockfish toàn lực, suy nghĩ khoảng 2 giây.", 20, 2000, 2800);

    public static IReadOnlyList<ChessDifficulty> All { get; } =
        [Easy, Medium, Hard, Expert];
}
