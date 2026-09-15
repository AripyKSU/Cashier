using System;
using CsvHelper.Configuration.Attributes;

/// <summary>진행 단계에서 세 외형 프리팹으로 향하는 참조. 구매 규칙은 FacilityData에 남긴다.</summary>
public sealed class StoreStageData
{
    [Name("idx")] public uint Idx { get; set; }
    [Name("store_stage")] public uint StoreStage { get; set; }
    [Name("world_prefab_resource_idx")] public uint WorldPrefabResourceIdx { get; set; }
    [Name("front_prefab_resource_idx")] public uint FrontPrefabResourceIdx { get; set; }
    [Name("top_view_prefab_resource_idx")] public uint TopViewPrefabResourceIdx { get; set; }

    /// <summary>PK, 단계 범위와 필수 리소스 종류를 검사한다.</summary>
    public void Validate()
    {
        if (Util.GetDataTableType(Idx) != DataTableType.StoreStage || Idx % 1000 == 0 || StoreStage < 1 || StoreStage > 3)
            throw new ArgumentException($"StoreStage PK={Idx}: idx/store_stage 오류");
        foreach (uint resource in new[] { WorldPrefabResourceIdx, FrontPrefabResourceIdx, TopViewPrefabResourceIdx })
            if (Util.GetDataTableType(resource) != DataTableType.Resource || resource % 1000 == 0)
                throw new ArgumentException($"StoreStage PK={Idx}: Resource FK={resource} 오류");
    }
}
