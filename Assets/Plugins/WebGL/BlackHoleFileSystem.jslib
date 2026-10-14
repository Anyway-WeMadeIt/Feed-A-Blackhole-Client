// 웹 빌드 전용. SaveLocation.Flush가 부른다.
// 메모리 파일 시스템의 변경(/idbfs 아래)을 브라우저 IndexedDB로 옮겨 새로고침·재방문 뒤에도 남게 한다.
mergeInto(LibraryManager.library, {
  BlackHoleSyncFileSystem: function () {
    FS.syncfs(false, function (error) {
      if (error) console.error("[Save] IndexedDB sync failed: " + error);
    });
  },
});
