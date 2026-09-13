using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-loaded callbacks run after Awake and before Start / the initial T-pose gate.
public static class AvatarSelectionController
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Pattern Test") return;
        string id = AvatarSelectionSettings.Load();
        GameObject prefab = AvatarCatalog.Load(id);
        if (prefab == null) prefab = AvatarCatalog.Load(AvatarCatalog.DefaultId);
        if (prefab == null)
        {
            Debug.LogError("Avatar prefab is missing; keeping the scene avatar.");
            return;
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (PatternPosePlayer player in root.GetComponentsInChildren<PatternPosePlayer>(true))
        {
            if (!ReplaceModel(player, prefab) && id != AvatarCatalog.DefaultId)
            {
                GameObject fallback = AvatarCatalog.Load(AvatarCatalog.DefaultId);
                if (fallback != null) ReplaceModel(player, fallback);
            }
        }
    }

    private static bool ReplaceModel(PatternPosePlayer player, GameObject prefab)
    {
        Animator oldAnimator = player.TargetAnimator;
        if (oldAnimator == null) oldAnimator = player.GetComponentInChildren<Animator>(true);
        if (oldAnimator == null) return false;

        Renderer[] oldRenderers = oldAnimator.GetComponentsInChildren<Renderer>(true);
        GameObject model = Object.Instantiate(prefab, oldAnimator.transform, false);
        model.name = "Selected Dance Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        Animator animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
        {
            Debug.LogError($"Invalid Humanoid avatar: {prefab.name}. Keeping the previous model.", player);
            model.SetActive(false);
            Object.Destroy(model);
            return false;
        }

        foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = oldAnimator.gameObject.layer;

        // Normalize the new model to the existing stage's visible height and floor.
        Renderer[] newRenderers = model.GetComponentsInChildren<Renderer>(true);
        if (TryGetBounds(oldRenderers, out Bounds oldBounds) &&
            TryGetBounds(newRenderers, out Bounds newBounds))
        {
            model.transform.localScale *= oldBounds.size.y / newBounds.size.y;
            if (TryGetBounds(newRenderers, out newBounds))
                model.transform.position += Vector3.up * (oldBounds.min.y - newBounds.min.y);
        }

        foreach (Renderer renderer in oldRenderers) renderer.enabled = false;
        oldAnimator.enabled = false;
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.enabled = true;
        // Keep the player, its tuning and external references; Start loads the selected pattern.
        player.SetTargetAnimator(animator, false);
        return true;
    }

    private static bool TryGetBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled) continue;
            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }
        return found && bounds.size.y > 0.001f;
    }
}
