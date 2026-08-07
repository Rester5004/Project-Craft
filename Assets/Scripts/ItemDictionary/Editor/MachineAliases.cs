namespace ProjectCraft.EditorTools
{
    /// <summary>
    /// 옛 기계 이름 → 현재 기계의 <b>표시 이름</b>. 레시피가 어느 기계에 붙는지를 정하는 유일한 표다.
    ///
    /// <b>왜 한 곳인가</b> — 예전에는 같은 표가 세 벌로 갈라져 있었고, 실제로 어긋났다.
    /// <c>MachineBlockFiller</c> 는 "수동 0-0티어 추출기 → 조합대" 를 <b>의도적으로 지웠는데</b>
    /// <c>RecipeTreeMerger</c> 에는 그 줄이 그대로 남아, 추출기가 정식 기계가 된 뒤에도
    /// 그 레시피가 조합대 그룹으로 병합됐다. <c>RecipeJsonImporter</c> 는 또 다른 세 줄만 갖고 있었다.
    /// <see cref="ItemAliases"/> 가 아이템에 대해 하는 일("한 표를 여러 곳이 함께 본다")을 기계에도 적용한다.
    ///
    /// 에디터 전용이다 — 런타임은 기계를 표시 이름이 아니라 <c>blockId</c> 로 찾으므로 이 표가 필요 없다
    /// (그쪽 옛 이름 호환은 <see cref="ItemAliases"/> 가 맡는다).
    /// </summary>
    public static class MachineAliases
    {
        /// <summary>옛 이름, 정본 표시 이름 순의 짝.</summary>
        private static readonly string[,] Table =
        {
            // ── Notion 개편 때의 이름 변경·통합 ──────────────────────────
            // '용광로 → 화로' 는 뺐다. 용광로가 Machine:BlastFurnace 로 정식 기계가 됐으므로
            // 화로로 보내면 그 레시피가 엉뚱한 기계에 붙는다(설계 JSON 도 "화로와 별개인 상위 제련 기계" 라고 적고 있다).
            { "유리 제조기", "화로" },
            { "가공대", "조합대" },
            { "철근 공장", "조합대" },
            { "파이프 공장", "조합대" },
            { "파이프 공장 (2티어 업그레이드)", "조합대" },
            { "망치", "조합대" },
            { "수전해기", "전기 분해기" },
            { "벽돌 공장", "압연기" },
            { "벽돌 공장 (1티어 업그레이드)", "시멘트 공장" },
            { "파이프 공장 (1티어 업그레이드)", "유리 가공기" },
            { "화력발전소", "화력 발전기" },
            { "화력발전소 (1티어 업그레이드)", "화력 발전기" },

            // ── JSON 표기 흔들림 ─────────────────────────────────────────
            { "분쇄기", "전기 분쇄기" },
            { "전기분해기", "전기 분해기" },

            // ⚠ 여기에 '수동 분쇄기 → 전기 분쇄기' 를 <b>다시 넣지 말 것.</b>
            //    Machine:ManualPulverizer 가 실재하는 기계가 됐으므로, 전기 쪽으로 보내면
            //    수동 사슬 레시피가 엉뚱한 기계에 붙는다.
            // ⚠ 여기에 '수동 0-0티어 추출기 → 조합대' 를 <b>다시 넣지 말 것.</b>
            //    Machine:Extractor00 이 실재하는 기계이므로, 조합대로 보내면 그 레시피가 엉뚱한 곳에 붙는다.
        };

        private static readonly System.Collections.Generic.Dictionary<string, string> map
            = new System.Collections.Generic.Dictionary<string, string>();

        static MachineAliases()
        {
            for (int i = 0; i < Table.GetLength(0); i++)
            {
                // 한글은 완성형/조합형이 겉보기엔 같아도 문자열 비교가 실패한다. 양쪽 다 NFC 로 맞춘다.
                string from = ItemDictionary.NormalizeName(Table[i, 0]);
                string to = ItemDictionary.NormalizeName(Table[i, 1]);
                if (!string.IsNullOrEmpty(from) && !map.ContainsKey(from)) map[from] = to;
            }
        }

        /// <summary>정본 표시 이름(별칭이 아니면 넘긴 이름을 NFC 로만 맞춰 그대로).</summary>
        public static string Resolve(string machineName)
        {
            if (string.IsNullOrEmpty(machineName)) return machineName;

            string key = ItemDictionary.NormalizeName(machineName);
            return map.TryGetValue(key, out string canonical) ? canonical : key;
        }

        /// <summary>표 전체(옛 이름 → 정본).</summary>
        public static System.Collections.Generic.IEnumerable<
            System.Collections.Generic.KeyValuePair<string, string>> All => map;

        public static int Count => map.Count;
    }
}
