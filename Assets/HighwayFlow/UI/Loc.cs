using System.Collections.Generic;
using UnityEngine;

namespace HighwayFlow.UI
{
    /// <summary>Tiny localisation table: the English text is the key, Korean is looked up.</summary>
    public static class Loc
    {
        const string PrefKey = "hf_lang";
        static bool? korean;
        public static event System.Action Changed;

        public static bool Korean
        {
            get { if (korean == null) korean = PlayerPrefs.GetInt(PrefKey, 1) == 1; return korean.Value; }
            set { korean = value; PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        static readonly Dictionary<string, string> Ko = new Dictionary<string, string>
        {
            ["+1 min"] = "+1분",
            ["1. Straight highway"] = "1. 직선 고속도로",
            ["2. Highway + on-ramp"] = "2. 고속도로 + 진입로",
            ["3. Exit, then on-ramp"] = "3. 진출로 → 진입로",
            ["4. On-ramp, then exit"] = "4. 진입로 → 진출로",
            ["5. Exit with traffic light"] = "5. 진출로 신호등",
            ["An on-ramp joins the highway gently through an acceleration lane."] = "진입로가 가속 차로를 통해 완만하게 고속도로에 합류합니다.",
            ["auto"] = "자동",
            ["Camera"] = "카메라",
            ["Delay"] = "지체",
            ["Distance between the two ramps (m)"] = "두 램프 사이 거리 (m)",
            ["Done"] = "완료",
            ["Efficiency"] = "효율",
            ["Efficiency (%)"] = "효율 (%)",
            ["Travel-time efficiency"] = "주행 효율 (자유주행 시간 / 실제 시간)",
            ["efficiency < 70% from"] = "효율 70% 미만 시작:",
            ["efficiency stays >= 70%"] = "효율 70% 이상 유지",
            ["Efficiency sweep: vehicles per minute"] = "효율 스윕: 분당 차량 수",
            ["Exit first"] = "진출로 먼저",
            ["Exit queue"] = "진출로 대기 차량",
            ["Exit ramp length (m)"] = "진출로 길이 (m)",
            ["Exit share (% of vehicles)"] = "진출 차량 비율 (%)",
            ["Follow a car"] = "차량 따라가기",
            ["From (veh/min)"] = "시작 (대/분)",
            ["Green"] = "녹색",
            ["Green share (%)"] = "녹색 비율 (%)",
            ["Highway traffic flow simulator"] = "고속도로 차량 흐름 시뮬레이터",
            ["Lane"] = "차선",
            ["Lanes"] = "차선 수",
            ["Language"] = "언어",
            ["Live"] = "실시간",
            ["Mean delay per trip"] = "이동당 평균 지체",
            ["Mean delay per trip (s)"] = "이동당 평균 지체 (초)",
            ["Mean speed"] = "평균 속도",
            ["Mean speed (km/h)"] = "평균 속도 (km/h)",
            ["Missed exits"] = "놓친 출구",
            ["Mouse: left-drag rotate, right-drag move, wheel zoom"] = "마우스: 왼쪽 드래그 회전, 오른쪽 드래그 이동, 휠 확대/축소",
            ["On-ramp first"] = "진입로 먼저",
            ["On-ramp flow (% of mainline)"] = "진입로 유입량 (본선 대비 %)",
            ["Order of the ramps"] = "램프 배치 순서",
            ["Pause"] = "일시정지",
            ["peak throughput"] = "최대 처리량",
            ["Plain straight highway: only through traffic."] = "직선 고속도로: 통과 차량만 있습니다.",
            ["Points"] = "측정 지점 수",
            ["Ramp area"] = "램프 구간",
            ["Red"] = "적색",
            ["Reset"] = "초기화",
            ["Road and traffic"] = "도로와 교통량",
            ["Role"] = "역할",
            ["Run sweep"] = "스윕 실행",
            ["Running…"] = "실행 중…",
            ["Runs all selected scenarios headless with increasing vehicles per minute and compares the efficiency."] = "선택한 시나리오를 분당 차량 수를 늘려가며 한꺼번에 시뮬레이션하고 효율을 비교합니다.",
            ["Scenario"] = "시나리오",
            ["Scenarios"] = "시나리오 선택",
            ["Seeds per point"] = "지점당 반복(시드)",
            ["Simulation"] = "시뮬레이션",
            ["Speed"] = "속도",
            ["Start"] = "시작",
            ["Stopped"] = "정지 비율",
            ["Stopped vehicle-time (%)"] = "정지(20km/h 미만) 차량 시간 (%)",
            ["Sweep result"] = "스윕 결과",
            ["The exit ramp ends in a traffic light. When the queue is longer than the ramp it blocks the highway."] = "진출로 끝에 신호등이 있습니다. 대기 행렬이 진출로보다 길어지면 고속도로까지 막힙니다.",
            ["Throughput"] = "처리량",
            ["Time"] = "시간",
            ["To (0 = automatic)"] = "끝 (0 = 자동)",
            ["Traffic light"] = "신호등",
            ["Traffic light cycle (s)"] = "신호 주기 (초)",
            ["Trips completed (last min)"] = "완료한 이동 (최근 1분)",
            ["Trips completed (veh/min)"] = "완료한 이동 (대/분)",
            ["veh/min"] = "대/분",
            ["Vehicle colour"] = "차량 색",
            ["Vehicles join first and the exit follows: short distances create a weaving section."] = "진입이 먼저고 진출이 뒤따릅니다. 거리가 짧으면 엇갈림(위빙) 구간이 생깁니다.",
            ["Vehicles leave at the exit first, then new vehicles join at the on-ramp."] = "차량이 먼저 진출로로 빠지고, 그 뒤 진입로에서 새 차량이 합류합니다.",
            ["Vehicles on the road"] = "도로 위 차량 수",
            ["Vehicles per minute (mainline)"] = "분당 차량 수 (본선)",
            ["Vehicles per minute entering the mainline"] = "본선에 투입되는 분당 차량 수",
            ["View"] = "보기",
            ["Waiting to enter (main / ramp)"] = "진입 대기 (본선 / 진입로)",
            ["Whole road"] = "도로 전체",
        };

        public static string T(string en)
        {
            if (Korean && Ko.TryGetValue(en, out var ko)) return ko;
            return en;
        }
    }
}
