using UnityEngine;

/// <summary>
/// Yön göstergesinin hangi diagonal yönü temsil ettiğini tutar.
/// Tıklama artık GameManager.Update() tarafından doğrudan ele alınıyor.
/// OnMouseDown / OnMouseEnter / OnMouseExit yeni Input System ile çalışmadığı için kaldırıldı.
/// </summary>
public class DirectionIndicator : MonoBehaviour
{
    public Vector2Int direction;
}
