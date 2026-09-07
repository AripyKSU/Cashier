using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>손님 관련 네 테이블과 공용 TextData를 함께 검증하여 공개한다.</summary>
public sealed class CustomerCatalog
{
    /// <summary>외형 CSV 로더와 공개 데이터.</summary>
    public CustomerCsvTable<CustomerAppearanceData> Appearances { get; } =
        new CustomerCsvTable<CustomerAppearanceData>("CustomerAppearanceData.csv", DataTableType.CustomerAppearance,
            x => x.Idx, x =>
            {
                if (x.ColorA == 0)
                    throw new InvalidDataException($"column=color_a, PK={x.Idx}: 1~255 필요");
            });
    /// <summary>성향 CSV 로더와 공개 데이터.</summary>
    public CustomerCsvTable<CustomerDispositionData> Dispositions { get; } =
        new CustomerCsvTable<CustomerDispositionData>("CustomerDispositionData.csv", DataTableType.CustomerDisposition,
            x => x.Idx, x => x.ValidatePurchaseSettings());
    /// <summary>상품군 CSV 로더와 공개 데이터.</summary>
    public CustomerCsvTable<ProductCategoryData> Categories { get; } =
        new CustomerCsvTable<ProductCategoryData>("ProductCategoryData.csv", DataTableType.ProductCategory,
            x => x.Idx, x =>
            {
                if (x.ProductType == ProductType.None || !Enum.IsDefined(typeof(ProductType), x.ProductType))
                    throw new InvalidDataException($"ProductCategoryData.csv PK={x.Idx}, product_type 오류");
            });
    /// <summary>상품 CSV 로더와 공개 데이터.</summary>
    public CustomerCsvTable<ProductData> Products { get; } =
        new CustomerCsvTable<ProductData>("ProductData.csv", DataTableType.Product,
            x => x.Idx, x => x.Validate());

    /// <summary>표시 문자열 테이블. 공용 DataTableType.Text에도 동일 로더를 등록한다.</summary>
    public CustomerCsvTable<TextData> Texts { get; } =
        new CustomerCsvTable<TextData>("TextData.csv", DataTableType.Text, x => x.Idx, x =>
        {
            if (string.IsNullOrWhiteSpace(x.Text))
                throw new InvalidDataException($"column=text, PK={x.Idx}: 필수값 누락");
        });

    /// <summary>다섯 테이블이 모두 준비된 뒤 FK 검증과 공개를 한 번에 수행한다.</summary>
    /// <exception cref="InvalidDataException">필수 테이블 또는 상품군 FK 누락.</exception>
    /// <param name="resources">이미지 FK가 있는 경우 필수인 검증된 리소스 테이블.</param>
    public void ValidateAndCommit(ResourceDataTable resources = null)
    {
        try
        {
            if (Appearances.PendingRows == null || Dispositions.PendingRows == null ||
                Categories.PendingRows == null || Products.PendingRows == null || Texts.PendingRows == null)
                throw new InvalidDataException("손님 CSV 4종과 TextData.csv가 필요합니다. Datas 라벨과 로더 등록을 확인하세요.");
            foreach (var row in Appearances.PendingRows.Values)
                validateNameReference("CustomerAppearanceData.csv", row.Idx, row.NameIdx);
            foreach (var row in Dispositions.PendingRows.Values)
                validateNameReference("CustomerDispositionData.csv", row.Idx, row.NameIdx);
            foreach (var row in Categories.PendingRows.Values)
                validateNameReference("ProductCategoryData.csv", row.Idx, row.NameIdx);
            foreach (var row in Products.PendingRows.Values)
                validateNameReference("ProductData.csv", row.Idx, row.NameIdx);
            var types = new HashSet<ProductType>();
            foreach (var row in Categories.PendingRows.Values)
                if (!types.Add(row.ProductType))
                    throw new InvalidDataException($"ProductCategoryData.csv PK={row.Idx}, product_type 중복");
            foreach (ProductType type in Enum.GetValues(typeof(ProductType)))
                if (type != ProductType.None && !types.Contains(type))
                    throw new InvalidDataException($"ProductCategoryData.csv: product_type={type} 표시 데이터 누락");
            foreach (var row in Products.PendingRows.Values)
            {
                if (!types.Contains(row.ProductType))
                    throw new InvalidDataException($"ProductData.csv PK={row.Idx}, product_type 표시 참조 실패");
                if (row.ImageResourceIdx.HasValue && (resources == null || !resources.TryGetResource(row.ImageResourceIdx.Value, out _)))
                    throw new InvalidDataException($"ProductData.csv PK={row.Idx}, image_resource_idx={row.ImageResourceIdx}: Resource 참조 실패");
            }
            foreach (var pair in Dispositions.PendingRows)
            {
                foreach (var type in pair.Value.PreferredProductTypes)
                    if (!types.Contains(type))
                        throw new InvalidDataException($"CustomerDispositionData.csv PK={pair.Key}, preferred_product_types 표시 참조 실패");
                foreach (uint textIdx in pair.Value.EntryTextIdxs.Concat(pair.Value.AcceptTextIdxs).Concat(pair.Value.RejectTextIdxs))
                    if (!Texts.PendingRows.ContainsKey(textIdx))
                        throw new InvalidDataException($"CustomerDispositionData.csv PK={pair.Key}, dialog text FK={textIdx} -> TextData.idx 참조 실패");
            }

            // 대기 중인 어느 테이블에도 문제가 없을 때만 runtime 소비자에게 공개한다.
            Appearances.Commit();
            Dispositions.Commit();
            Categories.Commit();
            Products.Commit();
            Texts.Commit();
        }
        catch (Exception exception)
        {
            Debug.LogError("[Customer CSV] " + exception.Message);
            throw;
        }
    }

    /// <summary>이름은 문자열이 아니라 TextData의 유효 PK여야 한다.</summary>
    /// <param name="file">원본 CSV.</param>
    /// <param name="idx">원본 행 PK.</param>
    /// <param name="nameIdx">TextData FK.</param>
    /// <exception cref="InvalidDataException">이름 참조 누락 또는 잘못된 대역.</exception>
    private void validateNameReference(string file, uint idx, uint nameIdx)
    {
        if (!Texts.PendingRows.ContainsKey(nameIdx))
            throw new InvalidDataException($"{file} PK={idx}, column=nameidx, FK={nameIdx} -> TextData.idx 참조 실패");
    }
}
