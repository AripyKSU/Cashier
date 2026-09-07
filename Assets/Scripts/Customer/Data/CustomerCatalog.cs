using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>손님 관련 네 테이블과 공용 TextData를 함께 검증하여 공개한다.</summary>
public sealed class CustomerCatalog
{
    /// <summary>외형 전용 테이블. DataTableManager에 등록된 인스턴스를 참조한다.</summary>
    public CustomerAppearanceDataTable Appearances { get; }
    /// <summary>성향 전용 테이블. DataTableManager에 등록된 인스턴스를 참조한다.</summary>
    public CustomerDispositionDataTable Dispositions { get; }
    /// <summary>상품 분류 전용 테이블. DataTableManager에 등록된 인스턴스를 참조한다.</summary>
    public ProductCategoryDataTable Categories { get; }
    /// <summary>상품 전용 테이블. DataTableManager에 등록된 인스턴스를 참조한다.</summary>
    public ProductDataTable Products { get; }
    /// <summary>Manager 소유 테이블을 참조하며 별도 테이블을 생성하지 않는다.</summary>
    /// <param name="appearances">등록된 외형 테이블.</param>
    /// <param name="dispositions">등록된 성향 테이블.</param>
    /// <param name="categories">등록된 상품 분류 테이블.</param>
    /// <param name="products">등록된 상품 테이블.</param>
    /// <exception cref="ArgumentNullException">필수 테이블 누락.</exception>
    public CustomerCatalog(CustomerAppearanceDataTable appearances, CustomerDispositionDataTable dispositions,
        ProductCategoryDataTable categories, ProductDataTable products)
    {
        Appearances = appearances ?? throw new ArgumentNullException(nameof(appearances));
        Dispositions = dispositions ?? throw new ArgumentNullException(nameof(dispositions));
        Categories = categories ?? throw new ArgumentNullException(nameof(categories));
        Products = products ?? throw new ArgumentNullException(nameof(products));
    }

    /// <summary>다섯 테이블이 모두 준비된 뒤 FK 검증과 공개를 한 번에 수행한다.</summary>
    /// <exception cref="InvalidDataException">필수 테이블 또는 상품군 FK 누락.</exception>
    /// <param name="resources">이미지 FK가 있는 경우 필수인 검증된 리소스 테이블.</param>
    /// <param name="texts">게임 전체 공용 텍스트 테이블. 검증 성공 시 함께 공개한다.</param>
    public void ValidateAndCommit(TextDataTable texts, ResourceDataTable resources = null)
    {
        try
        {
            if (texts == null || Appearances.PendingRows == null || Dispositions.PendingRows == null ||
                Categories.PendingRows == null || Products.PendingRows == null || texts.PendingRows == null)
                throw new InvalidDataException("손님 CSV 4종과 TextData.csv가 필요합니다. Datas 라벨과 로더 등록을 확인하세요.");
            foreach (var row in Appearances.PendingRows.Values)
                validateNameReference(texts, "CustomerAppearanceData.csv", row.Idx, row.NameIdx);
            foreach (var row in Dispositions.PendingRows.Values)
                validateNameReference(texts, "CustomerDispositionData.csv", row.Idx, row.NameIdx);
            foreach (var row in Categories.PendingRows.Values)
                validateNameReference(texts, "ProductCategoryData.csv", row.Idx, row.NameIdx);
            foreach (var row in Products.PendingRows.Values)
                validateNameReference(texts, "ProductData.csv", row.Idx, row.NameIdx);
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
                    if (!texts.PendingRows.ContainsKey(textIdx))
                        throw new InvalidDataException($"CustomerDispositionData.csv PK={pair.Key}, dialog text FK={textIdx} -> TextData.idx 참조 실패");
            }

            // 대기 중인 어느 테이블에도 문제가 없을 때만 runtime 소비자에게 공개한다.
            Appearances.Commit();
            Dispositions.Commit();
            Categories.Commit();
            Products.Commit();
            texts.Commit();
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
    /// <param name="texts">게임 전체 공용 텍스트 테이블.</param>
    private void validateNameReference(TextDataTable texts, string file, uint idx, uint nameIdx)
    {
        if (!texts.PendingRows.ContainsKey(nameIdx))
            throw new InvalidDataException($"{file} PK={idx}, column=nameidx, FK={nameIdx} -> TextData.idx 참조 실패");
    }
}
