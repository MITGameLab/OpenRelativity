using UnityEngine;

namespace PastInfinity
{
    public class TarotFaceSelector : MonoBehaviour
    {

        public int catalogCardIndex = 0;
        private int oldCatalogCardIndex;

        private MeshFilter meshFilter;
        private Mesh change;
        Vector2[] origUV;

        const int xTiles = 12;
        const int yTiles = 7;
        const float scaleX = 1.0f / xTiles;
        const float scaleY = (13720.0f / 13848.0f) / yTiles;
        const float offsetY = 128.0f / 13848.0f;

        // Use this for initialization, before relativistic object CombineParent() starts.
        void Awake()
        {
            oldCatalogCardIndex = catalogCardIndex;

            //Grab the meshfilter, and if it's not null, keep going
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                enabled = false;
                return;
            }

            //Prepare a new mesh for our split mesh
            change = Instantiate(meshFilter.mesh);

            origUV = new Vector2[change.uv.Length];
            change.uv.CopyTo(origUV, 0);

            UpdateUVs();
        }

        void UpdateUVs() {
            int xIndex = catalogCardIndex % xTiles;
            int yIndex = (yTiles - 1) - (catalogCardIndex / xTiles);

            Vector2[] nUv = new Vector2[change.uv.Length];
            for (int uvIndex = 0; uvIndex < nUv.Length; uvIndex++)
            {
                nUv[uvIndex] = Vector2.Scale(new Vector2(scaleX, scaleY), origUV[uvIndex]) + new Vector2(scaleX * xIndex, scaleY * yIndex + offsetY);
            }

            change.uv = nUv;
            meshFilter.mesh = change;
        }

        private void Update()
        {
            if (catalogCardIndex != oldCatalogCardIndex)
            {
                oldCatalogCardIndex = catalogCardIndex;
                UpdateUVs();
            }
        }
    }
}