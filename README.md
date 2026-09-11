### ⚠️ IMPORTANT NOTICE / DISCLAIMER

**Original Author:** Terkoiz
**Original Repository:** Skipper
**Original Link:** https://dev.sp-tarkov.com/Terkoiz/Skipper
**License:** MIT
**This Port By:** R_F (danyhappy564-cmyk) — unofficial, AI-assisted port. Not affiliated with or endorsed by the original author.

1. **Reflection & Take-Downs:** I deeply reflect on the ECOT incident. As an AI-assisted "vibe coder," I will immediately delete files if the original authors ask.
2. **No Re-Distribution:** These ported builds are unverified, temporary fixes. Please do NOT re-upload or share them anywhere else.
3. **Do Not Pester Original Authors:** Never report bugs or pester original modders regarding issues from my unofficial ports.
4. **Full Credit & Respect:** I will always credit original creators on GitHub and prioritize their decisions above all else.
5. **Support Original Creators:** Instead of using my ports, please visit the original authors' Forge pages to leave kind words or tips.

---

# Skipper (fork)

> **원작자 · 원본 레포**
> **Terkoiz** — https://dev.sp-tarkov.com/Terkoiz/Skipper
>
> **라이선스: MIT**
>
> 이 레포는 위 원작의 **포크**입니다. 원작은 퀘스트 목표를 무료로 스킵하는 플러그인이고,
> 여기서 추가한 건 **스킵에 비용을 매기는 것**뿐입니다.

퀘스트 창에서 목표 옆의 SKIP 버튼으로 해당 목표를 즉시 완료 처리합니다.
현재 기준 **SPT 4.1**.

---

## 이 포크가 추가한 것

**스킵할 때 돈이 나갑니다.** F12(BepInEx ConfigurationManager)의 `2. 비용` 섹션에서 전부
조절합니다. F12 항목과 인게임 문구는 한글입니다.

| 설정 | 하는 일 |
|---|---|
| `1. 무료로 스킵` | 켜면 무료. 아래 설정 전부 무시하고 서버에 연락도 안 합니다 |
| `2. 통화` | Roubles(루블) / Dollars(달러) / Euros(유로) |
| `3. 퀘스트 보상 기준으로 가격 산정` | 끄면 정액, 켜면 **퀘스트 보상 기준으로 자동 산정** |
| `4. 정액 요금` | 정액 요금 (0 ~ 50,000,000) |
| `5. 보상 대비 비율 (%)` | 보상 기준일 때, 퀘스트 전체 보상의 몇 %가 그 퀘스트를 다 스킵하는 값인지 (0 ~ 500%) |
| `6. 최소 요금` | 보상 기준 요금의 하한 |
| `7. 최대 요금` | 보상 기준 요금의 상한 (0 = 무제한) |

`2. 통화`의 선택지 이름(Roubles/Dollars/Euros)만 영문입니다 — 이 값은 서버로 그대로
전송되는 식별자라 번역하면 통화 인식이 깨집니다.

**스킵 확인창이 얼마 나가는지 먼저 알려줍니다:**

```
이 퀘스트 목표를 즉시 완료 처리할까요?

비용 45,000 ₽  ·  보유 2,310,000 ₽
```

돈이 모자라면 스킵이 **차단되고** 얼마가 필요한지 알려줍니다.

### 보상 기준 산정이 어떻게 계산되나

퀘스트가 성공 시 실제로 주는 것을 값으로 환산합니다:

```
퀘스트 가치(루블) = 경험치 × 100  +  보상 아이템의 핸드북 가격 합계
목표 하나당 요금  = 퀘스트 가치 × (Reward price percent / 100) ÷ 목표 개수
```

목표 개수로 나누는 게 핵심입니다. 퍼센트가 **목표 하나가 아니라 퀘스트 전체** 기준이라,
20%로 두면 목표 6개짜리 퀘스트는 한 단계당 약 3.3%씩 받고 전부 스킵하면 20%가 됩니다.

계산은 전부 루블로 하고 마지막에 선택한 통화로 환산합니다 — 핸드북이 루블로만 가격을
매기기 때문입니다. 반대로 했으면 달러 스킵에 루블 가격을 그대로 물려 145배쯤 더 받게
됩니다.

상인 평판만 주는 퀘스트처럼 값을 매길 게 없으면 정액 요금으로 넘어갑니다.

---

## 설치

**반쪽만 설치하면 안 됩니다.** 릴리스 zip을 SPT 루트에 그대로 풀면 둘 다 들어갑니다:

| 파일 | 위치 |
|---|---|
| `terkoiz-skipper.dll` | `BepInEx\plugins\Terkoiz.Skipper\` |
| `terkoiz-skipper-server.dll` | `SPT_Runtime\user\mods\Terkoiz.Skipper\` |

서버 쪽이 빠지면 플러그인이 `/skipper/charge`를 호출해도 받을 데가 없습니다. 그때는
조용히 무료로 넘어가지 않고 **안내창을 띄우고 스킵을 막습니다** — 설치가 덜 됐다는 걸
숨기는 것보다 낫기 때문입니다. 비용 없이 쓰고 싶으면 F12에서 `Skip for free`를 켜세요.

## 빌드

```
dotnet build project/Terkoiz.Skipper.sln
```

경로는 `TarkovDir`에서 나옵니다. 기본값 `E:\SPT 4.1\`, `-p:TarkovDir=...`로 덮어쓸 수
있고 클라이언트·서버 두 프로젝트가 같은 값을 씁니다. 빌드하면 양쪽 다 설치본으로 바로
복사되고, `project/Terkoiz.Skipper/release/`에 배포용 zip도 생깁니다.

---

## 왜 서버 모드가 필요한가

돈을 인벤토리에서 빼는 건 서버가 해야 합니다. 클라이언트에서 스택을 지우면 서버 프로필과
어긋나고, 그다음 스택을 옮기는 순간
`Unable to merge stacks as destination item ... cannot be found`로 터집니다.

SPT의 `PaymentService`를 쓰지 않은 이유도 있습니다 — 그 서비스의 두 진입점 모두 통화를
**상인에게서** 가져오기 때문에 달러나 유로로 정산할 수가 없습니다. 그래서 플레이어의
아이템 스택을 직접 걸어가는 방식(SPT Casino에서 검증된 것)을 가져왔습니다.

돈이 움직인 뒤에는 클라이언트가 아무것도 하지 않는 아이템 이벤트(`SkipperSync`)를 한 번
보냅니다. SPT는 세션의 프로필 변경분을 클라이언트의 다음 아이템 이벤트까지 들고 있다가
그 응답에 실어 보내므로, 이게 있어야 화면의 스택이 갱신됩니다.

---

## 한글 표기에 대해

F12 항목·설명과 인게임 확인창·안내창은 전부 한글입니다. 서버 콘솔 로그만 영문으로
남겨뒀습니다 — SPT 서버 로그의 다른 줄들과 같이 읽히는 편이 낫기 때문입니다.

SKIP 버튼 글자는 `SKIP` 그대로입니다. 이 버튼은 상인 화면의 인계 버튼을 복제해 만드는
것이라 폰트가 그 쪽을 따라가는데, 글리프가 없으면 네모로 보일 수 있어서 건드리지
않았습니다.

한글이 들어간 소스 파일에는 UTF-8 BOM을 넣어뒀습니다. BOM이 없으면 한글 로케일 윈도우의
컴파일러가 파일을 CP949로 읽어 문자열이 깨질 수 있습니다.

**F12 항목 이름이 바뀌었으므로**, 이전 버전을 이미 실행해서
`BepInEx\config\com.terkoiz.skipper.cfg`가 만들어져 있다면 예전 영문 키가 그대로 남고
새 한글 항목은 기본값으로 시작합니다. 설정 파일을 지우고 다시 만들거나, F12에서 값을
다시 지정하세요.
