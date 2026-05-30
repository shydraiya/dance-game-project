# body+droidcam+merge
MergedHumanoidPoseDriver.cs 가 추가
DroidCam 포즈는 webcam 포즈의 몸통 방향 기준으로 회전 정렬
정렬된 DroidCam 포즈와 webcam 포즈를 landmark 떄 합성
기본 weight:
webcam: 0.9
droidcam: 0.1
DroidCam visibility가 낮으면 DroidCam은 merge에서 제외하고 webcam만 사용
현재  0.5 주요 landmark 중 하나라도 0.5 이하이면 DroidCam weight를 0으로 처리--변경예정
따흐흑
