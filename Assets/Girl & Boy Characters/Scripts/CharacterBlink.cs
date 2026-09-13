using UnityEngine;
using System.Collections;

namespace Characters
{
    public class CharacterBlink : MonoBehaviour
    {
        [SerializeField] Texture2D baseTexture;
        [SerializeField] Texture2D closedEyesTexture;

        [SerializeField] string shaderMainTexName = "_MainTexture";
        [SerializeField] float timeBeforeBlink = 10f;

        Material bodyMat;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            bodyMat = GetComponent<SkinnedMeshRenderer>().material;

            StartCoroutine(Blink());
        }

        IEnumerator Blink()
        {
            bodyMat.SetTexture(shaderMainTexName, baseTexture);

            yield return new WaitForSeconds(timeBeforeBlink);

            bodyMat.SetTexture(shaderMainTexName, closedEyesTexture);

            yield return new WaitForSeconds(0.1f);

            StartCoroutine(Blink());
        }
    }
}
