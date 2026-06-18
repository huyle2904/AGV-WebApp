window.agvMap = {
  draw: function (canvasId, payload, dotNetRef) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
      return;
    }

    const ctx = canvas.getContext("2d");
    const entities = payload.entities || payload.Entities || [];
    const robots = payload.robots || payload.Robots || [];
    const showStations = payload.showStations ?? payload.ShowStations ?? true;
    const showPaths = payload.showPaths ?? payload.ShowPaths ?? true;
    const showZones = payload.showZones ?? payload.ShowZones ?? true;
    const showLabels = payload.showLabels ?? payload.ShowLabels ?? true;
    const zoom = Math.max(0.5, Math.min(2.5, payload.zoom ?? payload.Zoom ?? 1));
    const selectedEntityId = payload.selectedEntityId ?? payload.SelectedEntityId ?? "";
    const selectedRobotId = payload.selectedRobotId ?? payload.SelectedRobotId ?? "";
    const width = canvas.width;
    const height = canvas.height;
    const padding = 48;

    ctx.clearRect(0, 0, width, height);
    ctx.fillStyle = "#f8fafc";
    ctx.fillRect(0, 0, width, height);

    const visibleEntities = entities.filter((entity) => {
      const type = entity.type ?? entity.Type;
      if ((type === 0 || type === "Station") && !showStations) {
        return false;
      }
      if ((type === 1 || type === "Path") && !showPaths) {
        return false;
      }
      if ((type === 2 || type === "Zone") && !showZones) {
        return false;
      }
      return true;
    });

    const points = [];
    visibleEntities.forEach((entity) => {
      const x = Number(entity.x ?? entity.X ?? 0);
      const y = Number(entity.y ?? entity.Y ?? 0);
      const w = Math.max(0.3, Number(entity.width ?? entity.Width ?? 0.8));
      const h = Math.max(0.3, Number(entity.height ?? entity.Height ?? 0.8));
      points.push([x, y], [x + w, y + h]);
    });

    robots.forEach((robot) => {
      const pose = robot.pose ?? robot.Pose ?? {};
      points.push([
        Number(pose.x ?? pose.X ?? 0),
        Number(pose.y ?? pose.Y ?? 0)
      ]);
    });

    const bounds = getBounds(points);
    const fittedScale = Math.min(
      (width - padding * 2) / Math.max(1, bounds.maxX - bounds.minX),
      (height - padding * 2) / Math.max(1, bounds.maxY - bounds.minY)
    ) * zoom;
    const centerX = (bounds.minX + bounds.maxX) / 2;
    const centerY = (bounds.minY + bounds.maxY) / 2;

    const toCanvas = (x, y) => ({
      x: width / 2 + (x - centerX) * fittedScale,
      y: height / 2 - (y - centerY) * fittedScale
    });

    ctx.strokeStyle = "rgba(148, 163, 184, 0.22)";
    for (let x = 0; x <= width; x += 40) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, height);
      ctx.stroke();
    }

    for (let y = 0; y <= height; y += 40) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(width, y);
      ctx.stroke();
    }

    drawAxis(ctx, toCanvas, width, height);
    const hitBoxes = [];

    visibleEntities.forEach((entity) => {
      const worldX = Number(entity.x ?? entity.X ?? 0);
      const worldY = Number(entity.y ?? entity.Y ?? 0);
      const point = toCanvas(worldX, worldY);
      const rawW = Math.max(0.4, Number(entity.width ?? entity.Width ?? 0.8)) * fittedScale;
      const rawH = Math.max(0.4, Number(entity.height ?? entity.Height ?? 0.8)) * fittedScale;
      const x = point.x;
      const y = point.y;
      const w = Math.max(10, rawW);
      const h = Math.max(10, rawH);
      const type = entity.type ?? entity.Type;
      const id = entity.entityId ?? entity.EntityId;
      const selected = id === selectedEntityId;

      ctx.fillStyle = entity.color ?? entity.Color ?? "#38bdf8";
      ctx.strokeStyle = selected ? "#0f766e" : "rgba(15, 23, 42, 0.22)";
      ctx.lineWidth = selected ? 3 : 1.5;

      if (type === 2 || type === "Zone") {
        ctx.globalAlpha = 0.18;
        ctx.fillRect(x - w / 2, y - h / 2, w, h);
        ctx.globalAlpha = 1;
        ctx.strokeRect(x - w / 2, y - h / 2, w, h);
      } else if (type === 1 || type === "Path") {
        ctx.globalAlpha = 0.45;
        ctx.fillRect(x - w / 2, y - h / 2, w, h);
        ctx.globalAlpha = 1;
      } else {
        ctx.beginPath();
        ctx.roundRect(x - w / 2, y - h / 2, w, h, 6);
        ctx.fill();
        ctx.stroke();
        hitBoxes.push({
          id,
          x: x - Math.max(18, w) / 2,
          y: y - Math.max(18, h) / 2,
          w: Math.max(18, w),
          h: Math.max(18, h)
        });
      }

      if (showLabels) {
        ctx.fillStyle = "#334155";
        ctx.font = selected ? "bold 12px Segoe UI" : "12px Segoe UI";
        ctx.fillText(entity.name ?? entity.Name, x + 8, y - 8);
      }
    });

    robots.forEach((robot) => {
      const pose = robot.pose ?? robot.Pose ?? {};
      const point = toCanvas(Number(pose.x ?? pose.X ?? 0), Number(pose.y ?? pose.Y ?? 0));
      const x = point.x;
      const y = point.y;
      const heading = ((pose.headingDegrees ?? pose.HeadingDegrees) || 0) * (Math.PI / 180);
      const mode = robot.mode ?? robot.Mode;
      const robotId = robot.robotId ?? robot.RobotId;
      const fill = mode === 1 || mode === "Navigating" ? "#16a34a" : mode === 5 || mode === "Fault" ? "#dc2626" : "#f8fafc";
      const selected = !selectedRobotId || robotId === selectedRobotId;

      ctx.beginPath();
      ctx.arc(x, y, selected ? 13 : 10, 0, Math.PI * 2);
      ctx.fillStyle = fill;
      ctx.fill();
      ctx.strokeStyle = selected ? "#0f172a" : "#94a3b8";
      ctx.lineWidth = selected ? 3 : 2;
      ctx.stroke();

      ctx.beginPath();
      ctx.moveTo(x, y);
      ctx.lineTo(x + Math.cos(heading) * 16, y + Math.sin(heading) * 16);
      ctx.strokeStyle = "#38bdf8";
      ctx.lineWidth = 3;
      ctx.stroke();

      ctx.fillStyle = "#111827";
      ctx.font = "bold 12px Segoe UI";
      ctx.fillText(robot.name ?? robot.Name, x + 14, y - 12);
      ctx.font = "11px Segoe UI";
      ctx.fillText(`${robot.batteryPercent ?? robot.BatteryPercent}%`, x + 14, y + 2);
    });

    canvas._agvHitBoxes = hitBoxes;
    canvas.onclick = (event) => {
      if (!dotNetRef || !canvas._agvHitBoxes) {
        return;
      }

      const rect = canvas.getBoundingClientRect();
      const clickX = (event.clientX - rect.left) * (canvas.width / rect.width);
      const clickY = (event.clientY - rect.top) * (canvas.height / rect.height);
      const hit = canvas._agvHitBoxes.find((box) =>
        clickX >= box.x &&
        clickX <= box.x + box.w &&
        clickY >= box.y &&
        clickY <= box.y + box.h);

      if (hit?.id) {
        dotNetRef.invokeMethodAsync("SelectEntityFromCanvas", hit.id);
      }
    };
  }
};

function getBounds(points) {
  if (!points.length) {
    return { minX: -10, maxX: 10, minY: -6, maxY: 6 };
  }

  const xs = points.map((point) => point[0]);
  const ys = points.map((point) => point[1]);
  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);

  return {
    minX: minX === maxX ? minX - 5 : minX,
    maxX: minX === maxX ? maxX + 5 : maxX,
    minY: minY === maxY ? minY - 5 : minY,
    maxY: minY === maxY ? maxY + 5 : maxY
  };
}

function drawAxis(ctx, toCanvas, width, height) {
  const origin = toCanvas(0, 0);

  if (origin.x > 0 && origin.x < width) {
    ctx.strokeStyle = "rgba(239, 68, 68, 0.45)";
    ctx.beginPath();
    ctx.moveTo(origin.x, 0);
    ctx.lineTo(origin.x, height);
    ctx.stroke();
  }

  if (origin.y > 0 && origin.y < height) {
    ctx.strokeStyle = "rgba(34, 197, 94, 0.45)";
    ctx.beginPath();
    ctx.moveTo(0, origin.y);
    ctx.lineTo(width, origin.y);
    ctx.stroke();
  }
}
