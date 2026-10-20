namespace BlackHole.Net
{
    // 계정 정보를 읽고 쓴다. 실제 구현은 BlackHole.Net.Unity(진행 저장 폴더의 account.json, 계약 2.5).
    public interface ICredentialStore
    {
        // 없거나 내용을 읽지 못하면(깨진 형식) null. 읽지 못한 파일은 구현이 백업으로 옮긴다.
        // 파일 읽기 자체가 실패하면(잠김 등) 예외 — null이면 새 계정을 등록해 기존 파일을 덮어쓰기 때문이다.
        GuestCredentials Load();

        // 원자적으로 쓴다(임시 파일에 쓴 뒤 바꿔치기). 실패하면 예외.
        void Save(GuestCredentials credentials);
    }
}
