#!/bin/bash
INPUT=$(cat)
CONTENT=$(echo "$INPUT" | jq -r '.tool_input.content // .tool_input.new_string // empty')

JS_PATTERN='(^|\n)(export )?(async )?(function |const [A-Za-z]+ = (async )?\(|class [A-Za-z])'
CS_TYPE_PATTERN='(^|\n)[[:space:]]*(public|private|protected|internal)([[:space:]]+(static|sealed|abstract|partial|readonly))*[[:space:]]+(class|interface|record|struct|enum)[[:space:]]+[A-Za-z_]'
CS_METHOD_PATTERN='(^|\n)[[:space:]]*(public|private|protected|internal)([[:space:]]+(static|async|override|virtual|abstract|sealed|readonly|new))*[[:space:]]+[A-Za-z_][A-Za-z0-9_<>,.? \t]*[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*\('

if echo "$CONTENT" | grep -qE "$JS_PATTERN" \
  || echo "$CONTENT" | grep -qE "$CS_TYPE_PATTERN" \
  || echo "$CONTENT" | grep -qE "$CS_METHOD_PATTERN"; then
  cat <<'EOF'
{
  "hookSpecificOutput": {
    "hookEventName": "PreToolUse",
    "additionalContext": "Reminder: this edit looks like it introduces a new function, class, or component. Check .claude/catalog/backend.md and .claude/catalog/frontend.md to see if something equivalent already exists before adding new code."
  }
}
EOF
fi

exit 0