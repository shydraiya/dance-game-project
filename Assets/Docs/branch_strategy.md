# Git 브랜치 전략

## 브랜치 구조

```
main
└── develop
    ├── feature/*
    ├── fix/*
    └── refactor/*
```

---

## 브랜치 역할

### main

* 발표 및 제출 가능한 안정 버전
* 직접 Push 금지
* Release 시에만 Merge

---

### develop

* 기본 개발 브랜치
* 모든 기능이 Merge되는 브랜치

---

### feature/*

새로운 기능 개발

예시

```
feature/pose-detection
feature/score-system
feature/pause-menu
```

---

### fix/*

버그 수정

예시

```
fix/webcam-error
fix/pause-menu
```

---

### refactor/*

기능 변경 없이 코드 구조 개선

예시

```
refactor/game-manager
```

---

# 개발 절차

1. 최신 develop을 가져온다.

```bash
git checkout develop
git pull origin develop
```

2. 새로운 브랜치를 생성한다.

```bash
git checkout -b feature/기능이름
```

3. 작업 후 Commit한다.

```bash
git add .
git commit -m "feat: add pose scoring"
```

4. 원격 저장소에 Push한다.

```bash
git push origin feature/기능이름
```

5. GitHub에서 Pull Request를 생성한다.

```
feature/* → develop
```

6. 리뷰 후 Merge한다.

7. Merge가 완료되면 작업 브랜치를 삭제한다.

---

# Merge 규칙

* `main`에 직접 Push하지 않는다.
* 모든 작업은 Feature 브랜치에서 진행한다.
* Merge는 Pull Request를 통해 진행한다.
* Merge 전 최신 `develop`을 반영한다.
* 작업이 끝난 브랜치는 삭제한다.

---

# Commit Message 규칙

| 타입       | 설명         |
| -------- | ---------- |
| feat     | 새로운 기능     |
| fix      | 버그 수정      |
| refactor | 리팩터링       |
| docs     | 문서 수정      |
| style    | 코드 스타일 변경  |
| test     | 테스트 코드     |
| chore    | 설정 및 기타 작업 |

예시

```
feat: add pose scoring system
fix: prevent pause menu input
refactor: split GameManager
docs: update README
chore: update gitignore
```

---

# Unity 협업 규칙

* Scene과 Prefab은 동시에 여러 명이 수정하지 않는다.
* 수정하게되면 단톡방에 수정중이라고 공지하고 작업을 진행한다.
* 큰 작업을 시작하기 전에 팀원에게 공유한다.
* Merge 전에 프로젝트가 정상적으로 실행되는지 확인한다.
* Console Error가 없는 상태에서 Commit한다.
* 사용하지 않는 Asset과 Script는 Merge 전에 정리한다.

---

# Pull Request 체크리스트

* [ ] 프로젝트가 정상적으로 실행된다.
* [ ] Console Error가 없다.
* [ ] 불필요한 파일이 포함되지 않았다.
* [ ] 코드 컨벤션을 준수했다.
* [ ] 관련 기능을 직접 테스트했다.
* [ ] Merge 후 브랜치를 삭제했다.