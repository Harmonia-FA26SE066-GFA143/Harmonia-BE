#!/usr/bin/env bash
# SessionStart hook — in trạng thái repo THẬT để Claude không đọc mô tả đã mốc.
# stdout của hook được nạp vào context đầu phiên.

cd "$CLAUDE_PROJECT_DIR" || exit 0

echo "## Trạng thái repo (tự sinh lúc mở phiên)"
echo

if [ -d .git ]; then
  echo "Nhánh: $(git branch --show-current 2>/dev/null)"
  echo "5 commit gần nhất:"
  git log --oneline -5 2>/dev/null | sed 's/^/  /'
  dirty=$(git status --short 2>/dev/null | head -10)
  if [ -n "$dirty" ]; then
    echo "Đang sửa dở:"
    echo "$dirty" | sed 's/^/  /'
  fi
else
  echo "CHƯA git init."
fi

echo

empty=$(find src -name '*.cs' -size 0 -not -path '*/obj/*' -not -path '*/bin/*' 2>/dev/null | wc -l)
total=$(find src -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' 2>/dev/null | wc -l)
echo "File .cs: $total, trong đó rỗng 0 byte: $empty"

refs=$(grep -rl 'ProjectReference' --include='*.csproj' src 2>/dev/null | wc -l)
echo "Project có ProjectReference: $refs/4"

last_mig=$(ls -1 src/Harmonia.Infrastructure/Migrations/*.cs 2>/dev/null | grep -v Designer | tail -1)
if [ -n "$last_mig" ]; then
  echo "Migration mới nhất: $(basename "$last_mig")"
else
  echo "Chưa có migration nào."
fi

exit 0