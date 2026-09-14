#!/bin/bash
# Monitoring: disk and memory alerts, simple for one operator, no dedicated ops team
# Run via cron every hour: 0 * * * * /opt/learncloud/deployment/monitoring/check-disk-memory.sh

set -euo pipefail

# Config from env or defaults
DISK_THRESHOLD=80 # percent
MEM_THRESHOLD=85 # percent
SWAP_THRESHOLD=70
ALERT_EMAIL="${ALERT_EMAIL:-admin@learncloud.co.zw}"
SLACK_WEBHOOK="${SLACK_WEBHOOK:-}" # optional

HOSTNAME=$(hostname)
DATE=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

# Check disk
DISK_USAGE=$(df -h / | awk 'NR==2 {print $5}' | sed 's/%//')
DISK_AVAIL=$(df -h / | awk 'NR==2 {print $4}')

# Check memory
MEM_INFO=$(free | grep Mem)
MEM_TOTAL=$(echo $MEM_INFO | awk '{print $2}')
MEM_USED=$(echo $MEM_INFO | awk '{print $3}')
MEM_PERCENT=$(awk "BEGIN {printf \"%d\", $MEM_USED/$MEM_TOTAL*100}")

SWAP_INFO=$(free | grep Swap || echo "Swap 0 0 0")
SWAP_TOTAL=$(echo $SWAP_INFO | awk '{print $2}')
SWAP_USED=$(echo $SWAP_INFO | awk '{print $3}')
if [ "$SWAP_TOTAL" -gt 0 ]; then
  SWAP_PERCENT=$(awk "BEGIN {printf \"%d\", $SWAP_USED/$SWAP_TOTAL*100}")
else
  SWAP_PERCENT=0
fi

# Check docker containers health
UNHEALTHY=$(docker ps --filter health=unhealthy --format "{{.Names}}: {{.Status}}" 2>/dev/null || echo "")

# Logging structured JSON for rotation
LOG_FILE="/var/log/learncloud-monitoring.log"
mkdir -p /var/log 2>/dev/null || true

log_json() {
  echo "{\"time\":\"$DATE\",\"host\":\"$HOSTNAME\",\"disk_usage\":$DISK_USAGE,\"disk_avail\":\"$DISK_AVAIL\",\"mem_percent\":$MEM_PERCENT,\"swap_percent\":$SWAP_PERCENT,\"unhealthy\":\"$UNHEALTHY\"}" | tee -a "$LOG_FILE" 2>/dev/null || echo "{\"time\":\"$DATE\",\"disk\":$DISK_USAGE,\"mem\":$MEM_PERCENT}"
}

ALERT=false
ALERT_MSG="LearnCloud monitoring alert on $HOSTNAME at $DATE\n"

if [ "$DISK_USAGE" -ge "$DISK_THRESHOLD" ]; then
  ALERT_MSG+="DISK ALERT: ${DISK_USAGE}% used (threshold ${DISK_THRESHOLD}%), avail ${DISK_AVAIL}\n"
  ALERT=true
fi

if [ "$MEM_PERCENT" -ge "$MEM_THRESHOLD" ]; then
  ALERT_MSG+="MEMORY ALERT: ${MEM_PERCENT}% used (threshold ${MEM_THRESHOLD}%)\n"
  ALERT=true
fi

if [ "$SWAP_PERCENT" -ge "$SWAP_THRESHOLD" ]; then
  ALERT_MSG+="SWAP ALERT: ${SWAP_PERCENT}% used\n"
  ALERT=true
fi

if [ -n "$UNHEALTHY" ]; then
  ALERT_MSG+="UNHEALTHY CONTAINERS:\n$UNHEALTHY\n"
  ALERT=true
fi

# Uptime check - self check health endpoint
HEALTH_URL="${UPTIME_CHECK_URL:-http://localhost/health}"
if ! curl -f -s --max-time 10 "$HEALTH_URL" > /dev/null; then
  ALERT_MSG+="UPTIME ALERT: Health check failed for $HEALTH_URL\n"
  ALERT=true
fi

# Slow query log check - if slow queries > 10 in last hour, alert
SLOW_LOG="/var/log/mysql/slow.log"
if [ -f "$SLOW_LOG" ]; then
  SLOW_COUNT=$(find "$SLOW_LOG" -mmin -60 -exec wc -l {} \; 2>/dev/null | awk '{print $1}' || echo 0)
  if [ "$SLOW_COUNT" -gt 10 ]; then
    ALERT_MSG+="SLOW QUERY ALERT: $SLOW_COUNT slow query log lines in last hour, check $SLOW_LOG\n"
    ALERT=true
  fi
fi

log_json

if [ "$ALERT" = true ]; then
  echo -e "$ALERT_MSG"
  # Send email if mail configured
  if command -v mail >/dev/null 2>&1 && [ -n "$ALERT_EMAIL" ]; then
    echo -e "$ALERT_MSG" | mail -s "LearnCloud ALERT $HOSTNAME" "$ALERT_EMAIL" || true
  fi
  # Slack webhook
  if [ -n "$SLACK_WEBHOOK" ]; then
    curl -X POST -H 'Content-type: application/json' --data "{\"text\":\"$ALERT_MSG\"}" "$SLACK_WEBHOOK" || true
  fi
  # Also log to docker api logs for error tracking
  echo "$ALERT_MSG" | logger -t learncloud-monitoring || true
  exit 1
else
  echo "OK - Disk ${DISK_USAGE}%, Mem ${MEM_PERCENT}%, Swap ${SWAP_PERCENT}%, Unhealthy: ${UNHEALTHY:-none}"
  exit 0
fi
