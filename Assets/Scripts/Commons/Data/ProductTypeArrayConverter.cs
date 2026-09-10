using System;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;

/// <summary>기존 밑줄 구분 숫자 배열을 상품 분류 배열로 변환한다.</summary>
public sealed class ProductTypeArrayConverter : UIntArrayConverter
{
    /// <summary>각 원소의 enum 유효성을 확인한다. 빈 목록은 선호 없음이다.</summary>
    /// <param name="text">밑줄로 구분한 숫자 코드.</param>
    /// <param name="row">CSV 행.</param>
    /// <param name="memberMapData">컬럼 매핑.</param>
    /// <returns>상품 분류 배열.</returns>
    /// <exception cref="FormatException">정의되지 않은 상품 분류.</exception>
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        var values = (uint[])base.ConvertFromString(text, row, memberMapData);
        if (values.Any(x => x == 0 || !Enum.IsDefined(typeof(ProductType), x)))
            throw new FormatException($"preferred_product_types={text}: 잘못된 분류");
        return values.Select(x => (ProductType)x).ToArray();
    }
}
