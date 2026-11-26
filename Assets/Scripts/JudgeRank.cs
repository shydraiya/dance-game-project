// 판정·점수 계산·UI가 공유하는 등급이다. None은 미판정 상태이고 Miss부터 Perfect까지 실제 판정을 나타낸다.

public enum JudgeRank
{
    None,
    Miss,
    Bad,
    Good,
    Perfect
}