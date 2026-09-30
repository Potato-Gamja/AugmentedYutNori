# 윷놀이 UI · P0 스타터 에셋 조립 가이드

이 문서는 `import_settings.csv`의 81개 PNG를 Unity에서 어떤 순서로 겹치고 어떤 색을 입히는지만 정리합니다. 좌표와 크기는 모두 **1920×1080 Canvas 기준 표시 px**입니다(PNG 픽셀의 50%).

## 0. 공통 규칙

| 항목 | 값 |
|---|---|
| Canvas Scaler | Scale With Screen Size · 1920×1080 · Expand · Reference Pixels Per Unit **100** |
| 스프라이트 PPU | 전부 **200** → UI Image를 Set Native Size 하면 PNG 크기의 절반으로 표시됨 |
| 월드(SpriteRenderer) | 1 unit = 표시 100px. 256px 말 스프라이트 = 1.28 unit. Main Camera Orthographic Size 5.4 |
| Pivot | 전부 Center (0.5, 0.5) |
| Alpha Is Transparency | 켬 (흰색 틴트 원본의 가장자리 색 번짐 방지) |
| 장식 Image | Raycast Target **끔**. 눌러야 하는 것(버튼 Face)만 켬 |
| Image Type | 9-slice(CSV에 border가 있는 파일)는 Sliced. 테두리·링 파일은 Fill Center 켜 둬도 안쪽이 투명함. `chip_done`만 **Tiled** |
| Image.color | 표에 “틴트”라고 적힌 것만 색을 입힘. 나머지는 흰색(255,255,255,255) |
| 글자 | PNG에 글자·숫자 없음. 전부 TMP |

### 색 값

| 팀 | 기본 c | 밝은 l | 어두운 d | 옅은 p | ThrowButton 틴트 |
|---|---|---|---|---|---|
| 1 빨강 | #D6403C | #F29A8C | #922A26 | #F8DDD6 | **#E1433F** (225,67,63) |
| 2 파랑 | #2E6BD1 | #86ACEE | #1E478F | #DCE6F8 | **#3071DC** (48,113,220) |
| 3 초록 (추후) | #2A9460 | #80CCA4 | #1C6642 | #D9EFE2 | #2C9C65 (44, 156, 101) |
| 4 노랑 (추후) | #E0950F | #F6C764 | #8F5C00 | #FBEBC8 | #EC9D10 (236, 157, 16) |

기타: 경고 #E8850C · 위험 #D6403C · 글자 #3A2618 · 보조 #7A5C3E · 흐림 #A88E6E · 증가분 #B36B00 (팀 색 면 위 #FFE7A6) · 빽도 글자 #6B5B4A · 비활성 글자 #A89A84

ThrowButton 면은 회색(242)으로 구워져 있어 위 틴트를 곱하면 정확히 팀 기본색 c가 됩니다(검증: 빨강 → 214,64,60 / 파랑 → 46,107,209).

---

## 1. PlayerPanel (480×500) — 뒤 → 앞

좌표는 패널 왼쪽 위 모서리 기준(아래로 +y)입니다.

| # | 오브젝트 | 스프라이트 | 위치 / 크기 | 색 |
|---|---|---|---|---|
| 1 | Shadow | `panel_shadow` | 패널 중심에서 아래로 14, 576×596 | 흰색 (색 구워짐) |
| 2 | Glow *(활성만)* | `panel_glow` | 패널 중심, 576×596 | 팀 c, 알파 0.38 (0.25↔0.45, 2초 맥동) |
| 3 | Paper | `panel_paper` | Stretch 480×500 (아래 6은 두께) | 흰색 |
| 4 | ActiveBorder *(활성만)* | `player_active_border` | Stretch 480×500 | 팀 c |
| 5 | Header | `player_header` | (20,20) 440×64 | 활성 팀 c / 비활성 팀 p |
| 6 | EmblemBubble | `piece_tint_fill` | 중심 (52,52), **110×110** (보이는 원 지름 48) | 흰색 |
| 7 | Emblem | `emblem_circle`(빨강) / `emblem_diamond`(파랑) | 중심 (52,52), 24×24 | 팀 c |
| 8 | NameTMP / TeamTMP | — | 왼쪽 92, 중심 y 40 / 68 | 활성 흰색, 비활성 팀 d · Display_Turn 30 / Body_Label 18 |
| 9 | StatusTag | `pill_fill` | 활성 (272,34) 176×36 / 비활성 (364,34) 84×36 | 흰색 |
| 10 | ClockIcon *(활성만)* | `icon_clock` | 왼쪽 288, 세로 중앙, 24×24 | 팀 d |
| 11 | StatusTMP | — | 활성은 왼쪽 316, 비활성은 태그 중앙 | 팀 d · Display 21 |
| 12 | WaitingSlot ×4 | `slot_well` | (20+104·i, 128) 88×88 | 흰색 |
| 13 | └ Piece | `piece_{team}_body` | 슬롯 중앙, 128×128 | — |
| 14 | └ OnBoardGhost | `piece_tint_rim` | 슬롯 중앙, 128×128, “판 위” TMP 겹침 | #A88E6E 알파 0.6 |
| 15 | FinishedSlot ×4 | `slot_well` | (20+72·i, 260) 56×56 | 흰색 |
| 16 | └ Piece | `piece_{team}_body` | 슬롯 중앙, 84×84 | — |

대기 말은 슬롯에 Piece, 판 위에 나간 말은 OnBoardGhost, 남는 슬롯은 비워 둡니다. 증강 영역은 P1 에셋 대기 중입니다.

## 2. TurnBanner (760×80) — 뒤 → 앞

| # | 오브젝트 | 스프라이트 | 위치 / 크기 | 색 |
|---|---|---|---|---|
| 1 | Shadow | `panel_shadow` | 중심에서 아래로 10, 856×176 | 흰색 |
| 2 | Glow *(기본·위험 상태)* | `panel_glow` | 중심, 856×176 | 팀 c 알파 0.45. 위험(5초 이하)은 #D6403C, 알파 맥동 |
| 3 | Paper | `panel_paper` | Stretch 760×80 | 흰색 |
| 4 | Border | `turn_banner_border` | Stretch 760×80 | 팀 c → 10초 이하 #E8850C → 5초 이하 #D6403C |
| 5 | TurnOrderStrip | Horizontal Layout, 왼쪽 16 | 세로 중앙 | — |
| 5a | └ 현재 점 | `turn_dot_current_{team}` | 36×36 | — |
| 5b | └ 현재 문양 | `emblem_circle` / `emblem_diamond` | 점 중앙, 16×16 | 흰색 (노랑 팀은 #3A2618) |
| 5c | └ 대기 점 | `turn_dot_wait_{team}` | 36×36 | — |
| 5d | └ 화살표 | `turn_chevron` | 12×12, 점과 점 사이 좌우 여백 4 | #A88E6E |
| 5e | └ “다음” TMP | — | 다음 차례 점 아래, 14 | #A88E6E |
| 6 | TurnText | — | 중심 (428,27), 폭 460 | 팀 d · Display_Turn 32 |
| 7 | HintText | — | 중심 (428,59), 폭 460 | #3A2618 · Body_Hint 22 |
| 8 | Timer | 아래 3장 | 왼쪽 위 (684,8) 64×64 | — |

## 3. Timer (64×64) — 뒤 → 앞

| # | 오브젝트 | 스프라이트 | 크기 | 설정 |
|---|---|---|---|---|
| 1 | DangerGlow *(5초 이하만)* | `timer_danger_glow` | 96×96, 링 중심 | 틴트 #D6403C, 알파 0.8, 1초 맥동 |
| 2 | RingBG | `timer_ring_bg` | 64×64 | 흰색 (트랙과 안쪽 면이 구워짐) |
| 3 | RingFill | `timer_ring_fill` | 64×64 | Image Type **Filled** · Radial 360 · Origin Top · Clockwise · fillAmount = 남은 시간 / 전체 시간. 틴트: 팀 c → 10초 이하 #E8850C → 5초 이하 #D6403C |
| 4 | Number TMP | — | 중앙 | Display_Timer 26, #3A2618 |

증강 선택 화면의 80×80 타이머는 이 프리팹의 Scale 1.25로 씁니다.

---

## 4. ThrowButton (220×122)

구조

```
ThrowRoot (220×122, 그래픽 없음)
├─ HighlightGlow  Image · btn_throw_highlight · Sliced · 오프셋 사방 −44 (308×210) · Raycast 끔
└─ ThrowButton    Button (Navigation = None) · Target Graphic = Face
   ├─ Face        Image · btn_throw_normal · Sliced · color = 팀 틴트
   ├─ Icon        (선택) icon_throw · 32×32 · 흰색
   ├─ Label       TMP Display_Button 38 · 흰색 · 중심 (110,44)
   └─ SubLabel    TMP Body_Label 18 · 흰색 · 중심 (110,80)
```

Sprite Swap 연결

| 상태 | Sprite | Face.color | 비고 |
|---|---|---|---|
| Normal | `btn_throw_normal` | 팀 틴트 | |
| Highlighted | `btn_throw_hover` | 팀 틴트 | 5% 밝아짐 |
| Pressed | `btn_throw_pressed` | 팀 틴트 | 면이 4(표시) 내려가고 어두워짐. Label·SubLabel도 4 내림 |
| Disabled | `btn_throw_disabled` | **흰색** | 따뜻한 회색이 구워져 있어 틴트하면 탁해짐 |
| 현재 행동 강조 | Normal과 같음 | 팀 틴트 | HighlightGlow 켜기. 색 팀 c 알파 0.55, 알파 0.35↔0.75 (1.2초) |

Sprite Swap은 Image.color를 바꾸지 않으므로, `interactable`을 바꾸는 기존 코드에서 Face.color를 함께 바꿔야 합니다(비활성 → 흰색, 활성 → 팀 틴트). 코드를 건드리기 싫다면 `btn_throw_disabled`를 흰색 자식 Image(DisabledFace)로 두고 `interactable`에 맞춰 켜고 끄는 방법도 됩니다.

**Tint 대안(이미지 절감):** `btn_throw_normal` 한 장만 쓰고 Transition을 Color Tint로 둡니다. Face.color는 흰색으로 두고 ColorBlock에 Normal = 팀 틴트, Highlighted = 팀 틴트 ×1.06, Pressed = 팀 틴트 ×0.88, Disabled = (240,228,208)을 넣으면 hover·pressed·disabled 3장이 필요 없습니다. 눌림 때 면이 내려가는 표현은 사라집니다.

## 5. RethrowButton (220×122)

```
RethrowRoot (220×122)
├─ HighlightGlow  btn_rethrow_highlight · 오프셋 사방 −44 (308×210)
└─ RethrowButton  Button · Target Graphic = Face · EventTrigger
   ├─ Face          Image · btn_rethrow_normal · Sliced · 흰색
   ├─ Border        Image · btn_rethrow_border · Sliced · Stretch · 틴트 팀 c · Raycast 끔
   ├─ BorderPressed Image · btn_rethrow_border_pressed · Sliced · Stretch · 틴트 팀 d · 기본 비활성
   ├─ Icon          icon_rethrow · 30×30 · 왼쪽 12 · 틴트 팀 c
   ├─ Label         TMP Display_Button 29 · 팀 d · 중심 (126,44)
   └─ SubLabel      TMP Body_Label 18 · #7A5C3E · 중심 (110,80) · “1회 남음”
```

| 상태 | Face Sprite | 테두리 | 아이콘·글자 |
|---|---|---|---|
| Normal | `btn_rethrow_normal` | Border 켬 | 팀 c / 팀 d |
| Highlighted | `btn_rethrow_hover` | Border 켬 | 같음 |
| Pressed | `btn_rethrow_pressed` | **BorderPressed** 켬, Border 끔 | 4 내림 |
| Disabled | `btn_rethrow_disabled` | 둘 다 끔 | #A89A84, SubLabel “사용 완료” |
| 현재 행동 강조 | Normal과 같음 | Border 켬 | HighlightGlow 켬 |

Pressed의 테두리 교체는 스크립트 없이 EventTrigger로 합니다. PointerDown → Border.SetActive(false), BorderPressed.SetActive(true). PointerUp과 PointerExit → 반대로. Disabled에서 두 테두리를 끄는 것은 `interactable`을 바꾸는 코드에서 처리합니다.

---

## 6. StoredResultChip (134×52) + 체크 배지

체크 배지는 칩 이미지에 합쳐져 있지 않은 **별도 오브젝트**입니다.

```
ChipRoot (134×52, 그래픽 없음)
├─ Glow        chip_glow · Sliced · 166×84 중앙 · 틴트 팀 c 알파 0.55 · 소비 예정만
├─ Face        Image · 상태별 스프라이트 · Sliced
├─ Label       TMP (아래 참고)
└─ CheckBadge  badge_check_{team} · 32×32 · Anchor Top Right · Pivot (0.5,0.5) · 위치 (−2, −4) · 선택됨만
   └─ CheckIcon  icon_check · 16×16 · 중앙 · 흰색
```

배지가 칩 위로 13 삐져나오므로 칩 목록 컨테이너에 RectMask2D를 쓰지 말고 위쪽 여백을 16 이상 둡니다.

| 상태 | Face Sprite | 글자색 (기본 / 증가분) | 추가 |
|---|---|---|---|
| 기본 | `chip_normal` | #3A2618 / — | |
| 증가분 적용 | `chip_bonus` | #3A2618 / #B36B00 | |
| 빽도 | `chip_back` | #6B5B4A / — | |
| 선택됨 | `chip_selected_{team}` | 팀 d / #B36B00 | CheckBadge 켬 |
| 소비 예정 | `chip_consume_{team}` | 흰색 / #FFE7A6 | Glow 켬, 칩 Scale 1.06 (0.1초) |
| 소비 완료 | `chip_done` (**Tiled**, Fill Center 끔) | #A88E6E 취소선 | |
| 사용 불가 | 기본과 같음 | 같음 | CanvasGroup 알파 0.45 |

Label 배치: TMP 한 개, Rect는 좌우 12 · 위 0 · 아래 3(칩 아래 두께). 정렬 Center/Middle, Wrapping 끔. 기본값 28, 증가분 22를 리치 텍스트로 나눕니다.

```
도 1 <size=79%><color=#B36B00>(+1)</color></size>
```

두 개로 나누려면 Horizontal Layout Group(Spacing 4, Child Alignment Middle Center)에 TMP 두 개를 넣습니다.

---

## 7. PieceView — SpriteRenderer, Sorting Layer `Piece`, 자식 전부 Local Position (0,0)

모든 말 레이어는 256×256, Pivot Center, PPU 200이라 위치를 0으로 두면 자동으로 겹칩니다. 본체 면의 중심이 캔버스 중심(128,128)입니다.

| Order in Layer | 오브젝트 | 스프라이트 | 켜는 조건 / 설정 |
|---|---|---|---|
| 0 | Floor | `piece_{team}_floor` | 선택됨 |
| 1 | FloorShadow | `piece_floor_shadow` | 선택됨 |
| 2 | Glow | `piece_{team}_glow` | 선택 가능. 알파 0.35↔1.0 (1.2초). 다른 말이 선택되면 알파 0.35 고정 |
| 3 | Body | `piece_{team}_body` | 항상 |
| 7 | Ring | `piece_{team}_ring` | 선택됨 |
| 8 | CountBadge | `count_badge_{team}` | 업힌 말 2개 이상. 위치 (+0.216, +0.216) unit |
| 9 | CountText | TextMeshPro(3D) | “×2” · Display_Chip 16 · 팀 d |

- **선택됨:** PieceRoot Scale을 1.15로 올립니다(0.12초 튀어오름 후 정지). Floor·FloorShadow·Ring이 함께 커집니다. Glow는 끕니다.
- **공존:** PieceRoot Scale 0.73. 2팀은 (∓0.28, 0). 4팀은 팀 1 (−0.26,+0.26), 팀 2 (+0.26,+0.26), 팀 3 (+0.26,−0.26), 팀 4 (−0.26,−0.26).
- **팀 3·4(틴트 조립):** Body 대신 `piece_tint_base`(order 3, 팀 d) → `piece_tint_fill`(4, 팀 c) → `piece_tint_rim`(5, 흰색) → `emblem_*`(6, 흰색, 노랑 팀은 #3A2618). Glow·Ring·Floor는 `piece_neutral_*`에 팀 l / c / c를 틴트합니다.
- **정렬 수치:** 본체·Glow·Ring의 중심은 (128,128)로 일치합니다. Floor는 (128,182), FloorShadow는 (128,188)로 의도적으로 아래에 있습니다. 세 값 모두 x = 128입니다.

## 8. NodeView

Sorting Layer 순서: `Board`(NodeView 원본) → `Highlight`(목적지 링) → `Piece`(말) → `Badge`(배지).

| 순서 | 오브젝트 | 스프라이트 | 설정 |
|---|---|---|---|
| 1 | DestRing | `dest_{team}_normal` ↔ `dest_{team}_hover` | 128×128 (1.28 unit) 칸 중심. 기본은 Scale 1.0↔1.06 (1초). 마우스 오버는 hover 스프라이트로 교체, 맥동 멈춤. 가운데가 투명해 칸이 보임 |
| 2 | BadgeRow | — | 링 위에 가로로 나열, 배지 사이 6 (0.06 unit), 배지 아래 끝 = 링 위 끝 + 6 |
| 2a | └ BadgePill | Sliced SpriteRenderer, 높이 34 (0.34 unit), 폭 = 글자 폭 + 30 (아이콘 있으면 +22) | 결과·완주 `badge_result_{team}` · 잡기 `badge_capture` · 빽도 `badge_back` · 업기 `badge_carry_{team}` |
| 2b | └ Icon | `icon_capture` / `icon_carry` / `icon_finish` | 20×20 (Scale 0.625), 배지 왼쪽 13. 업기 아이콘은 팀 c 틴트, 나머지 흰색 |
| 2c | └ TextMeshPro(3D) | — | Display_Chip 20. 결과·잡기·완주 흰색, 빽도 #6B5B4A, 업기 팀 d |

오른쪽 세로줄 칸은 배지를 링 왼쪽에 세로로 쌓습니다(링 왼쪽 끝 −14, 배지 사이 6, 세로 중앙 정렬).

팀 3·4는 `badge_result_tint_fill`(팀 c 틴트) 위에 `badge_result_outline`(흰색)을 겹치고, 링은 `dest_neutral_*`에 팀 c를 틴트합니다.

---

## 9. 파일 개수

| 폴더 | 개수 |
|---|---|
| Common | 5 |
| Player | 3 |
| Buttons | 14 |
| Results | 12 |
| Pieces | 21 |
| Destinations | 17 |
| Turn | 6 |
| Timer | 3 |
| **합계** | **81** |

`.meta` 파일은 포함하지 않았습니다. 가져오기 설정은 `import_settings.csv`를 기준으로 Unity에서 지정하세요(border 순서는 Unity의 left, bottom, right, top).
