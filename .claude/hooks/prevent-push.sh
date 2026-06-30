#!/usr/bin/env bash
# .claude/hooks/prevent-push.sh
#
# Blocks the agent from running any command containing both "git" and "push".
 
INPUT=$(cat)
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')
 
if [[ "$COMMAND" == *git* && "$COMMAND" == *push* ]]; then
  echo "git push is not allowed. Commit your changes and let the human push manually." >&2
  exit 2
fi
 
exit 0