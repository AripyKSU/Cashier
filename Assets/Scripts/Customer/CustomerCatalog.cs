using System;
using System.IO;
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
            x => x.Idx, x => { });
    /// <summary>상품 CSV 로더와 공개 데이터.</summary>
    public CustomerCsvTable<ProductData> Products { get; } =
        new CustomerCsvTable<ProductData>("ProductData.csv", DataTableType.Product,
            x => x.Idx, x => { });

    /// <summary>표시 문자열 테이블. 공용 DataTableType.Text에도 동일 로더를 등록한다.</summary>
    public CustomerCsvTable<TextData> Texts { get; } =
        new CustomerCsvTable<TextData>("TextData.csv", DataTableType.Text, x => x.Idx, x =>
        {
            if (string.IsNullOrWhiteSpace(x.Text))
                throw new InvalidDataException($"column=text, PK={x.Idx}: 필수값 누락");
        });

    /// <summary>다섯 테이블이 모두 준비된 뒤 FK 검증과 공개를 한 번에 수행한다.</summary>
    /// <exception cref="InvalidDataException">필수 테이블 또는 상품군 FK 누락.</exception>
    public void ValidateAndCommit()
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
            foreach (var pair in Products.PendingRows)
                if (!Categories.PendingRows.ContainsKey(pair.Value.CategoryIdx))
                    throw new InvalidDataException($"ProductData.csv PK={pair.Key}, column=category_idx, FK={pair.Value.CategoryIdx} -> ProductCategoryData.idx 참조 실패");
            foreach (var pair in Dispositions.PendingRows)
                foreach (uint category in pair.Value.PreferredCategoryIds)
                    if (!Categories.PendingRows.ContainsKey(category))
                        throw new InvalidDataException($"CustomerDispositionData.csv PK={pair.Key}, column=preferred_category_ids, FK={category} -> ProductCategoryData.idx 참조 실패");

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
