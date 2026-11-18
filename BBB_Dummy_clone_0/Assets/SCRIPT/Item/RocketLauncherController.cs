using Fusion;
using UnityEngine;

public class RocketLauncherController : NetworkBehaviour
{
    [Header("発射設定")]
    [SerializeField] private NetworkPrefabRef rocketProjectilePrefab;
    [SerializeField] private Transform muzzlePosition;

    [Header("効果音・エフェクト")]
    [SerializeField] private AudioSource soundSource;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private GameObject muzzleFlashPrefab;

    public void FireWeapon(NetworkRunner runner, PlayerRef inputAuthority, bool hasStateAuthority,Vector3 aimDirection)
    {
        Debug.Log("[RocketLauncherController] FireWeapon 実行");

        if (!rocketProjectilePrefab.IsValid)
        {
            Debug.LogError("エラー: rocketProjectilePrefab が未割り当てです！");
            return;
        }

        Vector3 spawnPos = muzzlePosition != null ? muzzlePosition.position : transform.position;
        Quaternion spawnRot = aimDirection != Vector3.zero 
            ? Quaternion.LookRotation(aimDirection) 
            : (muzzlePosition != null ? muzzlePosition.rotation : transform.rotation);

        // 渡された runner を使用して生成
        if (runner != null)
        {
            if (hasStateAuthority)
            {
                runner.Spawn(rocketProjectilePrefab, spawnPos, spawnRot, inputAuthority);
                Debug.Log("[RocketLauncherController] ロケット弾を正常にSpawnしました！");
            }
        }

        if (muzzleFlashPrefab != null) Instantiate(muzzleFlashPrefab, spawnPos, spawnRot);
        if (soundSource != null && fireSound != null) soundSource.PlayOneShot(fireSound);
    }
}