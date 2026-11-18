using Fusion;
using UnityEngine;

// クライアントからサーバーへ送るキー操作データ
public struct NetworkInputData : INetworkInput
{
    public Vector3 movementInput;
    public Vector3 aimDirection;   // カメラの前方向（Wキーで向く用）
    public NetworkBool isJumpPressed; // Spaceキーによるジャンプ
    
    public NetworkBool isWPressed; // Wキーが押されているか
    public NetworkBool isAPressed; // Aキーが押されているか
    public NetworkBool isDPressed; // Dキーが押されているか
    
    public NetworkBool isCharging; // 左クリック長押しによる溜め判定
    public NetworkBool isAiming;   // 右クリック長押し（軌道予測線）

    // ★ 追加：ロケットランチャー等の発射入力 (PlayerMovement.cs 211行目で使用)
    public NetworkBool isFirePressed;
    
    public NetworkBool cancelThrow; // Fキー（手放す）
    public NetworkBool interact;    // Eキー（拾う）

    public int selectedSlot;       // 現在選択中のスロット (0, 1, 2)
    public NetworkBool useItem;    // アイテム使用入力（左クリック）
}