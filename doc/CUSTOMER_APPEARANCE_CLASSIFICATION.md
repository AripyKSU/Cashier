# 손님 외형 이름 정리 참고표

2026-09-13. 이미지45종을 직접 확인한 외형 분류 초안이다. 성별은 기존 리소스의 Female/Male 구분을 따르고, 연령은 얼굴·체형의 표현과 사용자가 지정한 예시를 기준으로 아동·성인·노년으로 분류했다. 정확한 나이를 확정한 설정은 아니다. 성인에는 청년·중년을 포함한다.

의복의 낡음, 표정, 부유함·가난함·절박함 등 거래 성향은 분류하거나 이름에 넣지 않았다. FemaleCustomer_15는 아이를 안은 성인 여성 본인을 기준으로 분류했다. 원본 파일명·이미지·PK·이름 FK·이미지 FK를 유지하고 기존 TextData의 외형 이름45개만 교체했다.

성별·연령은 표시 이름을 정리할 때 참고한 인상이며, 외형의 고정 설정이나 데이터 검증 기준이 아니다. 추가했던 `gender,age` 컬럼·DTO 필드·로더 검증·분류 테스트는 사용자 지시에 따라 제거했다. CSV는 기존 `idx,nameidx,image_resource_idx` 3열을 사용한다.

현재 생성기는 성별·연령과 외형을 독립적으로 추첨한다. 이 참고표를 바탕으로 생성 조건을 고정하거나 일치를 강제하지 않는다.

| 성별 | 아동 | 성인 | 노년 | 합계 |
|---|---:|---:|---:|---:|
| 여성 | 2 | 11 | 5 | 18 |
| 남성 | 2 | 22 | 3 | 27 |

「연령 검토」는 청년/아동 또는 중년/노년의 표현이 모호한 이미지다. 표시 이름을 정리할 때 참고한 표현이며 필요하면 이름을 수정할 수 있다. Female01·Female02·Female17은 사용자가 제시한 분류를 그대로 적용했다.

| 외형 PK | 표시 이름 | 원본 이미지 | 검토 |
|---|---|---|---|
| 5001 | 성인 여성 01 | [FemaleCustomer_01](../Assets/Textures/Customer/Dystopia/FemaleCustomer_01.png) | 사용자 예시 |
| 5002 | 노년 여성 02 | [FemaleCustomer_02](../Assets/Textures/Customer/Dystopia/FemaleCustomer_02.png) | 사용자 예시 |
| 5003 | 성인 여성 03 | [FemaleCustomer_03](../Assets/Textures/Customer/Dystopia/FemaleCustomer_03.png) | 초안 |
| 5004 | 성인 여성 04 | [FemaleCustomer_04](../Assets/Textures/Customer/Dystopia/FemaleCustomer_04.png) | 초안 |
| 5005 | 성인 여성 05 | [FemaleCustomer_05](../Assets/Textures/Customer/Dystopia/FemaleCustomer_05.png) | 연령 검토 |
| 5006 | 성인 여성 06 | [FemaleCustomer_06](../Assets/Textures/Customer/Dystopia/FemaleCustomer_06.png) | 초안 |
| 5007 | 성인 여성 08 | [FemaleCustomer_08](../Assets/Textures/Customer/Dystopia/FemaleCustomer_08.png) | 초안 |
| 5008 | 성인 여성 09 | [FemaleCustomer_09](../Assets/Textures/Customer/Dystopia/FemaleCustomer_09.png) | 초안 |
| 5009 | 성인 여성 10 | [FemaleCustomer_10](../Assets/Textures/Customer/Dystopia/FemaleCustomer_10.png) | 초안 |
| 5010 | 노년 여성 11 | [FemaleCustomer_11](../Assets/Textures/Customer/Dystopia/FemaleCustomer_11.png) | 연령 검토 |
| 5011 | 성인 여성 12 | [FemaleCustomer_12](../Assets/Textures/Customer/Dystopia/FemaleCustomer_12.png) | 초안 |
| 5012 | 성인 여성 14 | [FemaleCustomer_14](../Assets/Textures/Customer/Dystopia/FemaleCustomer_14.png) | 초안 |
| 5013 | 성인 여성 15 | [FemaleCustomer_15](../Assets/Textures/Customer/Dystopia/FemaleCustomer_15.png) | 초안 |
| 5014 | 노년 여성 16 | [FemaleCustomer_16](../Assets/Textures/Customer/Dystopia/FemaleCustomer_16.png) | 연령 검토 |
| 5015 | 여자아이 17 | [FemaleCustomer_17](../Assets/Textures/Customer/Dystopia/FemaleCustomer_17.png) | 사용자 예시 |
| 5016 | 여자아이 18 | [FemaleCustomer_18](../Assets/Textures/Customer/Dystopia/FemaleCustomer_18.png) | 초안 |
| 5017 | 노년 여성 19 | [FemaleCustomer_19](../Assets/Textures/Customer/Dystopia/FemaleCustomer_19.png) | 초안 |
| 5018 | 노년 여성 20 | [FemaleCustomer_20](../Assets/Textures/Customer/Dystopia/FemaleCustomer_20.png) | 초안 |
| 5019 | 성인 남성 01 | [MaleCustomer_01](../Assets/Textures/Customer/Dystopia/MaleCustomer_01.png) | 초안 |
| 5020 | 성인 남성 02 | [MaleCustomer_02](../Assets/Textures/Customer/Dystopia/MaleCustomer_02.png) | 초안 |
| 5021 | 성인 남성 03 | [MaleCustomer_03](../Assets/Textures/Customer/Dystopia/MaleCustomer_03.png) | 연령 검토 |
| 5022 | 성인 남성 04 | [MaleCustomer_04](../Assets/Textures/Customer/Dystopia/MaleCustomer_04.png) | 초안 |
| 5023 | 성인 남성 05 | [MaleCustomer_05](../Assets/Textures/Customer/Dystopia/MaleCustomer_05.png) | 초안 |
| 5024 | 성인 남성 06 | [MaleCustomer_06](../Assets/Textures/Customer/Dystopia/MaleCustomer_06.png) | 초안 |
| 5025 | 성인 남성 07 | [MaleCustomer_07](../Assets/Textures/Customer/Dystopia/MaleCustomer_07.png) | 연령 검토 |
| 5026 | 성인 남성 08 | [MaleCustomer_08](../Assets/Textures/Customer/Dystopia/MaleCustomer_08.png) | 초안 |
| 5027 | 성인 남성 09 | [MaleCustomer_09](../Assets/Textures/Customer/Dystopia/MaleCustomer_09.png) | 초안 |
| 5028 | 성인 남성 10 | [MaleCustomer_10](../Assets/Textures/Customer/Dystopia/MaleCustomer_10.png) | 초안 |
| 5029 | 성인 남성 12 | [MaleCustomer_12](../Assets/Textures/Customer/Dystopia/MaleCustomer_12.png) | 초안 |
| 5030 | 성인 남성 13 | [MaleCustomer_13](../Assets/Textures/Customer/Dystopia/MaleCustomer_13.png) | 초안 |
| 5031 | 성인 남성 14 | [MaleCustomer_14](../Assets/Textures/Customer/Dystopia/MaleCustomer_14.png) | 초안 |
| 5032 | 성인 남성 15 | [MaleCustomer_15](../Assets/Textures/Customer/Dystopia/MaleCustomer_15.png) | 연령 검토 |
| 5033 | 성인 남성 16 | [MaleCustomer_16](../Assets/Textures/Customer/Dystopia/MaleCustomer_16.png) | 초안 |
| 5034 | 성인 남성 17 | [MaleCustomer_17](../Assets/Textures/Customer/Dystopia/MaleCustomer_17.png) | 초안 |
| 5035 | 성인 남성 18 | [MaleCustomer_18](../Assets/Textures/Customer/Dystopia/MaleCustomer_18.png) | 초안 |
| 5036 | 성인 남성 19 | [MaleCustomer_19](../Assets/Textures/Customer/Dystopia/MaleCustomer_19.png) | 초안 |
| 5037 | 성인 남성 21 | [MaleCustomer_21](../Assets/Textures/Customer/Dystopia/MaleCustomer_21.png) | 초안 |
| 5038 | 성인 남성 22 | [MaleCustomer_22](../Assets/Textures/Customer/Dystopia/MaleCustomer_22.png) | 초안 |
| 5039 | 성인 남성 23 | [MaleCustomer_23](../Assets/Textures/Customer/Dystopia/MaleCustomer_23.png) | 초안 |
| 5040 | 노년 남성 24 | [MaleCustomer_24](../Assets/Textures/Customer/Dystopia/MaleCustomer_24.png) | 연령 검토 |
| 5041 | 남자아이 25 | [MaleCustomer_25](../Assets/Textures/Customer/Dystopia/MaleCustomer_25.png) | 초안 |
| 5042 | 남자아이 26 | [MaleCustomer_26](../Assets/Textures/Customer/Dystopia/MaleCustomer_26.png) | 초안 |
| 5043 | 노년 남성 28 | [MaleCustomer_28](../Assets/Textures/Customer/Dystopia/MaleCustomer_28.png) | 초안 |
| 5044 | 노년 남성 29 | [MaleCustomer_29](../Assets/Textures/Customer/Dystopia/MaleCustomer_29.png) | 초안 |
| 5045 | 성인 남성 00 | [MaleCustomer0](../Assets/Textures/Customer/Dystopia/MaleCustomer0.png) | 초안 |

## 컬럼 제거 검증

- 기존45행의 PK·nameidx·image_resource_idx를 유지했다. TextData의 정리한 이름, 이미지 파일·GUID·Addressables는 이번 컬럼 제거에서 수정하지 않았다.
- 성별·연령을 고정하는 테스트12건을 제거했으며, 기존 CSV·PK/FK 테스트로 3열 데이터 로딩을 확인한다.
- 컬럼 제거 후 Unity 컴파일 완료, EditMode249/249 통과(실패·skip·미완료0). 증거: `Temp/TestResults/appearance-columns-removed-01/EditMode.xml`. `git diff --check` 통과. 이번 변경의 PlayMode·화면 검증과 커밋·푸시는 수행하지 않았다.
