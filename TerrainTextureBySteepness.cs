using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class TerrainTextureBySteepness : EditorWindow
{
    [MenuItem("Tools/Terrain Texture by Steepness")]
    public static void Init()
    {
        var window = EditorWindow.CreateInstance<TerrainTextureBySteepness>();
        window.titleContent = new GUIContent("Texture by Steepness");
        window.maxSize = new Vector2(400, 400);
        window.minSize = new Vector2(300, 300);
        window.ShowUtility();
        window.Focus();

        slope_max = 60;
    }

    private static int flat_terrain_layer = 0;
    private static int slope_terrain_layer = 0;
    private static float slope_max = 0;
    Texture2D steepness_map_texture_result;

    private void OnGUI()
    {
        if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Terrain>())
        {
            Terrain terrain = Selection.activeGameObject.GetComponent<Terrain>();

            if (GUILayout.Button("Resolve"))
                if (EditorUtility.DisplayDialog("Warning!", "Doing this will remove any detail that is already on the terrain.", "Sounds good"))
                    PaintTerrain(terrain);

            Color def = GUI.color;
            GUI.color = Color.green;
            GUILayout.Label("Selected Object: " + ((Selection.activeGameObject != null) ? Selection.activeGameObject.name : "null"));
            GUI.color = def;

            string[] options = new string[terrain.terrainData.terrainLayers.Length];
            for (int i = 0; i < options.Length; i++)
                options[i] = terrain.terrainData.terrainLayers[i].name;

            slope_terrain_layer = EditorGUILayout.Popup("Slope Layer", Mathf.Clamp(slope_terrain_layer, 0, options.Length - 1), options);
            flat_terrain_layer = EditorGUILayout.Popup("Flats Layer", Mathf.Clamp(flat_terrain_layer, 0, options.Length - 1), options);
            slope_max = EditorGUILayout.Slider("Slope", slope_max, 0, 90);

            if (GUILayout.Button("Preview Steepness Texture"))
                steepness_map_texture_result = CreateSteepnessTexture(terrain.terrainData);

            if (steepness_map_texture_result != null)
                GUILayout.Box(steepness_map_texture_result, GUILayout.Width(100), GUILayout.Height(100));

            GUILayout.Label("As the Slope increases the slope layer will cover more area.", 
                EditorStyles.helpBox);
        }

        Repaint();
    }

    private void PaintTerrain(Terrain terrain)
    { 
        TerrainData data = terrain.terrainData;

        if (flat_terrain_layer > data.terrainLayers.Length || slope_terrain_layer > data.terrainLayers.Length) return;

        float[,,] map = new float[data.alphamapWidth,data.alphamapHeight,data.alphamapLayers];
        var steepness_map = CreateSteepnessTexture(data);

        // creates alpha map
        for (int x = 0; x < map.GetLength(0); x++)
        {
            for (int y = 0; y < map.GetLength(1); y++)
            {
                if (steepness_map.GetPixel(y, x).grayscale < (slope_max / 90))
                    map[x, y, slope_terrain_layer] = 1f;
                else
                    map[x, y, flat_terrain_layer] = 1f;
            }
        }

        data.SetAlphamaps(0, 0, map);
        terrain.terrainData = data;
    }

    private Texture2D CreateSteepnessTexture(TerrainData data)
    {
        var steepness_map = new Texture2D((int)data.size.x, (int)data.size.y);

        //creates steepness map
        for (int x = 0; x < (int)data.size.x; x++)
        {
            for (int y = 0; y < (int)data.size.y; y++)
            {
                float steepness_point_x = x / data.size.x;
                float steepness_point_y = y / data.size.y;
                float steepness_clamped = (-data.GetSteepness(steepness_point_x, steepness_point_y) + 90) / 90f;

                steepness_map.SetPixel(x, y, new Color(steepness_clamped, steepness_clamped, steepness_clamped));
            }
        }

        ScaleImage(steepness_map, data.alphamapWidth, data.alphamapHeight);
        steepness_map.Apply();
        
        return steepness_map;
    }

    private static void GPUScaleImage(Texture2D src, int width, int height, FilterMode fmode)
    {
        //We need the source texture in VRAM because we render with it
        src.filterMode = fmode;
        src.Apply(true);

        //Using RTT for best quality and performance. Thanks, Unity 5
        RenderTexture rtt = new RenderTexture(width, height, 32);

        //Set the RTT in order to render to it
        Graphics.SetRenderTarget(rtt);

        //Setup 2D matrix in range 0..1, so nobody needs to care about sized
        GL.LoadPixelMatrix(0, 1, 1, 0);

        //Then clear & draw the texture to fill the entire RTT.
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        Graphics.DrawTexture(new Rect(0, 0, 1, 1), src);
    }

    private static void ScaleImage(Texture2D tex, int width, int height)
    {
        Rect texR = new Rect(0, 0, width, height);
        GPUScaleImage(tex, width, height, tex.filterMode);

        // Update new texture
        tex.Resize(width, height);
        tex.ReadPixels(texR, 0, 0, true);
    }
}
