using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// 스프라이트 시트에서 <b>노멀맵 시트</b>를 구워 낸다. 원본의 휘도를 높이로 보고 Sobel 로 기울기를 잡는다.
    ///
    /// <b>재실행 안전</b>하다 — 이미 있으면 덮어쓰고 임포트 설정을 다시 맞춘다.
    ///
    /// ⚠ <b>원본 시트를 다시 슬라이스하지 않는다.</b> 노멀은 원본과 <b>같은 크기의 별도 PNG</b> 한 장이고,
    /// 슬라이스는 메인 텍스처 것을 그대로 쓴다(Secondary Texture 규약). 그래서 CLAUDE.md §2 가 경고한
    /// <c>SpriteRect.spriteID</c> 유실 함정에 걸리지 않는다.
    ///
    /// ⚠ 원본의 <c>isReadable</c> 을 건드리지 않는다 — PNG 바이트를 직접 읽어 임시 텍스처로 올린다.
    /// 임포터를 만지면 그것만으로 meta 가 다시 쓰이므로, 손댈 이유가 없을 때는 손대지 않는 것이 맞다.
    /// </summary>
    public static class NormalMapGenerator
    {
        private const string SourceFolder = "Assets/Asset/MachineImages";
        private const string OutputFolder = SourceFolder + "/Normals";
        private const string Suffix = "_n";

        /// <summary>
        /// Secondary Texture 의 이름. <b>URP 셰이더가 이 이름 하나로 찾는다</b> —
        /// <c>Sprite-Lit-Default.shader:7</c> 의 <c>_NormalMap("Normal Map", 2D) = "bump"</c>.
        /// 한 글자만 달라도 조용히 무시되고 그림은 평평한 채로 남는다.
        /// </summary>
        private const string NormalMapProperty = "_NormalMap";

        /// <summary>
        /// 기울기를 얼마나 과장할지. <b>이 툴의 유일한 손잡이다.</b>
        ///
        /// ⚠ 픽셀 아트에는 <b>이미 명암이 그려져 있다</b>. 그것을 높이로 오해하는 방식이라
        /// 값을 키우면 칠해 둔 음영이 굴곡으로 두 번 강조돼 금세 과장된다 — 낮게 시작한다.
        /// </summary>
        private const float Strength = 1.5f;

        [MenuItem("Tools/Project Craft/Art/기계 노멀맵 생성")]
        public static void GenerateMachineNormals()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("# 기계 노멀맵 생성");
            report.AppendLine();
            report.AppendLine($"세기(Strength) = {Strength}");
            report.AppendLine();

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder(SourceFolder, "Normals");

            List<string> sources = new List<string>();
            foreach (string path in Directory.GetFiles(SourceFolder, "*.png", SearchOption.TopDirectoryOnly))
                sources.Add(path.Replace('\\', '/'));
            sources.Sort();

            int made = 0;
            foreach (string source in sources)
            {
                string outPath = $"{OutputFolder}/{Path.GetFileNameWithoutExtension(source)}{Suffix}.png";
                if (Bake(source, outPath, report)) made++;
            }

            AssetDatabase.Refresh();

            report.AppendLine();
            report.AppendLine($"원본 {sources.Count}장 → 노멀 {made}장 (`{OutputFolder}`)");
            report.AppendLine("이어서 `Tools/Project Craft/Art/기계 노멀맵 배선` 을 돌려야 실제로 쓰인다.");
            Debug.Log(report.ToString());
        }

        /// <summary>원본 한 장을 노멀 한 장으로 굽는다.</summary>
        private static bool Bake(string sourcePath, string outPath, StringBuilder report)
        {
            byte[] bytes = File.ReadAllBytes(sourcePath);

            // 임포트 설정과 무관하게 원본 픽셀을 그대로 얻는다(isReadable 을 켤 필요가 없다).
            Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(bytes))
            {
                report.AppendLine($"- ⚠ `{sourcePath}` 를 읽지 못했습니다.");
                Object.DestroyImmediate(src);
                return false;
            }

            int w = src.width, h = src.height;
            Color32[] pixels = src.GetPixels32();
            Object.DestroyImmediate(src);

            // 높이 = 휘도. 투명한 곳은 "높이가 없다" 로 두고 아래에서 이웃 클램프로 처리한다.
            float[] height = new float[w * h];
            bool[] solid = new bool[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                solid[i] = c.a > 0;
                height[i] = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            }

            Color32[] outPixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!solid[i])
                    {
                        // 투명한 곳은 평면. 여기에 굴곡을 넣으면 기계 바깥 허공이 빛을 받는다.
                        outPixels[i] = new Color32(128, 128, 255, 255);
                        continue;
                    }

                    float tl = At(height, solid, w, h, x - 1, y + 1, i);
                    float t  = At(height, solid, w, h, x,     y + 1, i);
                    float tr = At(height, solid, w, h, x + 1, y + 1, i);
                    float l  = At(height, solid, w, h, x - 1, y,     i);
                    float r  = At(height, solid, w, h, x + 1, y,     i);
                    float bl = At(height, solid, w, h, x - 1, y - 1, i);
                    float b  = At(height, solid, w, h, x,     y - 1, i);
                    float br = At(height, solid, w, h, x + 1, y - 1, i);

                    float dx = (tr + 2f * r + br) - (tl + 2f * l + bl);
                    float dy = (tl + 2f * t + tr) - (bl + 2f * b + br);

                    Vector3 n = new Vector3(-dx * Strength, -dy * Strength, 1f).normalized;
                    outPixels[i] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 0, 255),
                        255);
                }
            }

            Texture2D dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.SetPixels32(outPixels);
            dst.Apply();
            File.WriteAllBytes(outPath, dst.EncodeToPNG());
            Object.DestroyImmediate(dst);

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            ApplyImportSettings(outPath);

            report.AppendLine($"- `{Path.GetFileName(sourcePath)}` {w}×{h} → `{Path.GetFileName(outPath)}`");
            return true;
        }

        /// <summary>
        /// 이웃 높이를 읽되 <b>텍스처 밖이거나 투명하면 자기 자신으로 클램프</b>한다.
        ///
        /// ⚠ 이 클램프가 없으면 알파 경계에서 높이가 1 → 0 으로 떨어져 기울기가 폭발하고,
        /// 기계마다 <b>흰 테두리</b>가 두르게 된다.
        /// </summary>
        private static float At(float[] height, bool[] solid, int w, int h, int x, int y, int self)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return height[self];
            int i = y * w + x;
            return solid[i] ? height[i] : height[self];
        }

        /// <summary>
        /// 노멀 텍스처의 임포트 설정. <b>여섯 값이 전부 맞아야</b> 제대로 보인다.
        /// </summary>
        private static void ApplyImportSettings(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.NormalMap;

            // ⚠ 우리가 <b>이미 노멀로 구웠다.</b> 켜 두면 Unity 가 이 노멀을 다시 그레이스케일로 보고
            //    한 번 더 변환해 완전히 다른 그림이 된다.
            importer.convertToNormalmap = false;

            importer.sRGBTexture = false;                                   // 노멀은 색이 아니라 방향이다
            importer.filterMode = FilterMode.Point;                         // 프로젝트 전역 규칙(PPU 32 · Point)
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;                                 // 시트가 704 까지라 줄어들면 안 된다

            importer.SaveAndReimport();
        }

        /// <summary>
        /// 구워 둔 노멀을 원본 시트의 <b>Secondary Texture</b> 로 등록한다.
        ///
        /// 이름은 반드시 <c>_NormalMap</c> 이어야 한다 — URP 의
        /// <c>Sprite-Lit-Default.shader:7</c> 이 <c>_NormalMap("Normal Map", 2D)</c> 로 선언한 그 자리다.
        ///
        /// ⚠ 이것은 <b>텍스처 단위</b> 속성이라 <c>SpriteRect</c> 목록을 다시 쓰지 않는다.
        /// CLAUDE.md §2 가 경고하는 참조 유실은 <c>TextureImporter.spritesheet</c> 에 쓸 때 생기는 것이고
        /// (SpriteMetaData 에 spriteID 필드가 없다), 여기는 그 경로를 타지 않는다.
        ///
        /// <b>재실행 안전</b> — 이미 같은 노멀이 걸려 있으면 건너뛴다.
        /// </summary>
        [MenuItem("Tools/Project Craft/Art/기계 노멀맵 배선")]
        public static void WireMachineNormals()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("# 기계 노멀맵 배선 (Secondary Texture `_NormalMap`)");
            report.AppendLine();

            int wired = 0, skipped = 0, missing = 0;
            List<string> sources = new List<string>();
            foreach (string path in Directory.GetFiles(SourceFolder, "*.png", SearchOption.TopDirectoryOnly))
                sources.Add(path.Replace('\\', '/'));
            sources.Sort();

            foreach (string source in sources)
            {
                string normalPath = $"{OutputFolder}/{Path.GetFileNameWithoutExtension(source)}{Suffix}.png";
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normal == null)
                {
                    report.AppendLine($"- ⚠ `{Path.GetFileName(source)}` — 노멀 `{Path.GetFileName(normalPath)}` 이 없습니다. 생성을 먼저 돌리세요.");
                    missing++;
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(source) as TextureImporter;
                if (importer == null) { missing++; continue; }

                UnityEngine.SecondarySpriteTexture[] existing = importer.secondarySpriteTextures;
                bool already = false;
                for (int i = 0; i < existing.Length; i++)
                    if (existing[i].name == NormalMapProperty && existing[i].texture == normal) already = true;

                if (already)
                {
                    report.AppendLine($"- `{Path.GetFileName(source)}` — 이미 배선됨");
                    skipped++;
                    continue;
                }

                UnityEngine.SecondarySpriteTexture entry = new UnityEngine.SecondarySpriteTexture();
                entry.name = NormalMapProperty;
                entry.texture = normal;
                importer.secondarySpriteTextures = new UnityEngine.SecondarySpriteTexture[] { entry };
                importer.SaveAndReimport();

                report.AppendLine($"- `{Path.GetFileName(source)}` ← `{Path.GetFileName(normalPath)}`");
                wired++;
            }

            AssetDatabase.Refresh();
            report.AppendLine();
            report.AppendLine($"배선 {wired}장 · 이미 되어 있음 {skipped}장 · 노멀 없음 {missing}장");
            Debug.Log(report.ToString());
        }
    }
}
