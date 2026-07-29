namespace HotelCore.Application.Common.Messaging;

/// <summary>
/// "Değer döndürmeyen" use-case'ler için boş yanıt tipi. <c>Task</c> ve <c>Task&lt;T&gt;</c>
/// için ayrı boru hattı yazmamak adına her handler bir değer döndürür.
/// </summary>
public readonly record struct Unit
{
    /// <summary>Tek ve değersiz örnek.</summary>
    public static Unit Value => default;
}
