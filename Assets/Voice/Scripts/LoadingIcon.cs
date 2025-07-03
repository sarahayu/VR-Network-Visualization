using UnityEngine;

public class LoadingIcon : MonoBehaviour
{
    public float rotationSpeed = 100f;
    public bool loading = false;
    public GameObject text;
    private void Start()
    {
        if (!loading)
        {
            gameObject.SetActive(false);
            text.SetActive(false);
        }
    }

    void Update()
    {
        if (loading) transform.Rotate(Vector3.forward, -rotationSpeed * Time.deltaTime); // Rotate the icon
    }

    public void SetLoading(bool isLoading) // If loading then make it active, else make it inactive
    {
        loading = isLoading;
        gameObject.SetActive(loading);
        text.SetActive(loading);
    }
}
