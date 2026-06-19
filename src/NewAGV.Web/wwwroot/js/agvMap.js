window.agvMap = {
  setBackground: function (canvasId, background) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
      return;
    }

    canvas._agvBackground = normalizeBackground(background);
  },

  draw: function (canvasId, payload, dotNetRef) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
      return;
    }

    canvas._agvPayload = payload;
    canvas._agvDotNetRef = dotNetRef;
    ensureResizeObserver(canvasId, canvas);

    const viewport = ensureCanvasViewport(canvas);
    const ctx = canvas.getContext("2d");
    ctx.setTransform(viewport.dpr, 0, 0, viewport.dpr, 0, 0);
    ctx.imageSmoothingEnabled = true;
    ctx.lineJoin = "round";
    ctx.lineCap = "round";

    const background = normalizeBackground(payload.backgroundMap ?? payload.BackgroundMap) || canvas._agvBackground || null;
    canvas._agvBackground = background;
    const entities = payload.entities || payload.Entities || [];
    const robots = payload.robots || payload.Robots || [];
    const showStations = payload.showStations ?? payload.ShowStations ?? true;
    const showPaths = payload.showPaths ?? payload.ShowPaths ?? true;
    const showZones = payload.showZones ?? payload.ShowZones ?? true;
    const showLabels = payload.showLabels ?? payload.ShowLabels ?? true;
    const zoom = Math.max(0.5, Math.min(2.5, payload.zoom ?? payload.Zoom ?? 1));
    const selectedEntityId = payload.selectedEntityId ?? payload.SelectedEntityId ?? "";
    const selectedRobotId = payload.selectedRobotId ?? payload.SelectedRobotId ?? "";
    const width = viewport.width;
    const height = viewport.height;
    const padding = 36;

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

    const bounds = getWorldBounds(background, visibleEntities, robots);
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

    drawSurface(ctx, width, height);
    drawGrid(ctx, width, height);

    const hitBoxes = [];
    visibleEntities.forEach((entity) => {
      const type = entity.type ?? entity.Type;
      const id = entity.entityId ?? entity.EntityId;
      const name = entity.name ?? entity.Name ?? id;
      const worldX = Number(entity.x ?? entity.X ?? 0);
      const worldY = Number(entity.y ?? entity.Y ?? 0);
      const point = toCanvas(worldX, worldY);
      const selected = id === selectedEntityId;

      if (type === 0 || type === "Station") {
        const boxWidth = selected ? 48 : 42;
        const boxHeight = selected ? 30 : 26;
        const radius = 8;
        const left = roundToHalf(point.x - boxWidth / 2);
        const top = roundToHalf(point.y - boxHeight / 2);

        ctx.beginPath();
        ctx.roundRect(left, top, boxWidth, boxHeight, radius);
        ctx.fillStyle = selected ? "#0f766e" : "#14b8a6";
        ctx.fill();
        ctx.strokeStyle = "#ffffff";
        ctx.lineWidth = 2;
        ctx.stroke();

        hitBoxes.push({
          id,
          x: left - 4,
          y: top - 4,
          w: boxWidth + 8,
          h: boxHeight + 8
        });

        if (showLabels) {
          drawLabel(ctx, name, left + boxWidth + 12, top + 5, selected ? 14 : 13, true);
        }
      }
    });

    robots.forEach((robot) => {
      const pose = robot.pose ?? robot.Pose ?? {};
      const point = toCanvas(Number(pose.x ?? pose.X ?? 0), Number(pose.y ?? pose.Y ?? 0));
      const x = roundToHalf(point.x);
      const y = roundToHalf(point.y);
      const heading = ((pose.headingDegrees ?? pose.HeadingDegrees) || 0) * (Math.PI / 180);
      const mode = robot.mode ?? robot.Mode;
      const robotId = robot.robotId ?? robot.RobotId;
      const selected = !selectedRobotId || robotId === selectedRobotId;
      const fill = mode === 1 || mode === "Navigating"
        ? "#f97316"
        : mode === 5 || mode === "Fault"
          ? "#dc2626"
          : "#f59e0b";

      ctx.beginPath();
      ctx.arc(x, y, selected ? 13 : 11, 0, Math.PI * 2);
      ctx.fillStyle = fill;
      ctx.fill();
      ctx.strokeStyle = "#fff7ed";
      ctx.lineWidth = selected ? 3 : 2;
      ctx.stroke();

      const noseLength = selected ? 20 : 16;
      const leftAngle = heading + Math.PI * 0.8;
      const rightAngle = heading - Math.PI * 0.8;
      ctx.beginPath();
      ctx.moveTo(x + Math.cos(heading) * noseLength, y - Math.sin(heading) * noseLength);
      ctx.lineTo(x + Math.cos(leftAngle) * 8, y - Math.sin(leftAngle) * 8);
      ctx.lineTo(x + Math.cos(rightAngle) * 8, y - Math.sin(rightAngle) * 8);
      ctx.closePath();
      ctx.fillStyle = "#111827";
      ctx.fill();

      drawLabel(ctx, robot.name ?? robot.Name, x + 18, y - 14, 15, true);
      drawLabel(ctx, `${robot.batteryPercent ?? robot.BatteryPercent}%`, x + 18, y + 3, 13, false);
    });

    canvas._agvHitBoxes = hitBoxes;
    canvas.onclick = (event) => {
      if (!dotNetRef || !canvas._agvHitBoxes) {
        return;
      }

      const rect = canvas.getBoundingClientRect();
      const clickX = event.clientX - rect.left;
      const clickY = event.clientY - rect.top;
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

function ensureResizeObserver(canvasId, canvas) {
  if (canvas._agvResizeObserver) {
    return;
  }

  const redraw = () => {
    if (!canvas._agvPayload || !canvas._agvDotNetRef) {
      return;
    }

    if (canvas._agvResizeFrame) {
      cancelAnimationFrame(canvas._agvResizeFrame);
    }

    canvas._agvResizeFrame = requestAnimationFrame(() => {
      window.agvMap.draw(canvasId, canvas._agvPayload, canvas._agvDotNetRef);
    });
  };

  canvas._agvResizeObserver = new ResizeObserver(redraw);
  canvas._agvResizeObserver.observe(canvas);
}

function ensureCanvasViewport(canvas) {
  const rect = canvas.getBoundingClientRect();
  const width = Math.max(1, Math.round(rect.width));
  const height = Math.max(1, Math.round(rect.height));
  const dpr = Math.max(1, window.devicePixelRatio || 1);
  const pixelWidth = Math.round(width * dpr);
  const pixelHeight = Math.round(height * dpr);

  if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
    canvas.width = pixelWidth;
    canvas.height = pixelHeight;
  }

  return { width, height, dpr };
}

function normalizeBackground(background) {
  if (!background) {
    return null;
  }

  const bounds = background.bounds ?? background.Bounds;
  if (!bounds) {
    return null;
  }

  return {
    name: background.name ?? background.Name ?? "AGV Map",
    bounds: {
      minX: Number(bounds.minX ?? bounds.MinX ?? -10),
      minY: Number(bounds.minY ?? bounds.MinY ?? -6),
      maxX: Number(bounds.maxX ?? bounds.MaxX ?? 10),
      maxY: Number(bounds.maxY ?? bounds.MaxY ?? 6)
    }
  };
}

function getWorldBounds(background, entities, robots) {
  if (background?.bounds) {
    return background.bounds;
  }

  const points = [];
  entities.forEach((entity) => {
    points.push([
      Number(entity.x ?? entity.X ?? 0),
      Number(entity.y ?? entity.Y ?? 0)
    ]);
  });

  robots.forEach((robot) => {
    const pose = robot.pose ?? robot.Pose ?? {};
    points.push([
      Number(pose.x ?? pose.X ?? 0),
      Number(pose.y ?? pose.Y ?? 0)
    ]);
  });

  return getBounds(points);
}

function drawSurface(ctx, width, height) {
  const gradient = ctx.createLinearGradient(0, 0, 0, height);
  gradient.addColorStop(0, "#f8fbfc");
  gradient.addColorStop(1, "#e5eef2");
  ctx.clearRect(0, 0, width, height);
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, width, height);

  ctx.fillStyle = "rgba(148, 163, 184, 0.08)";
  ctx.beginPath();
  ctx.arc(width * 0.2, height * 0.15, 120, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(width * 0.84, height * 0.82, 170, 0, Math.PI * 2);
  ctx.fill();
}

function drawGrid(ctx, width, height) {
  ctx.strokeStyle = "rgba(148, 163, 184, 0.14)";
  ctx.lineWidth = 1;

  for (let x = 0; x <= width; x += 48) {
    const lineX = roundToHalf(x);
    ctx.beginPath();
    ctx.moveTo(lineX, 0);
    ctx.lineTo(lineX, height);
    ctx.stroke();
  }

  for (let y = 0; y <= height; y += 48) {
    const lineY = roundToHalf(y);
    ctx.beginPath();
    ctx.moveTo(0, lineY);
    ctx.lineTo(width, lineY);
    ctx.stroke();
  }
}

function drawLabel(ctx, text, x, y, fontSize, bold) {
  ctx.font = `${bold ? "700" : "600"} ${fontSize}px Segoe UI`;
  ctx.textBaseline = "top";
  ctx.lineWidth = 4;
  ctx.strokeStyle = "rgba(248, 250, 252, 0.96)";
  ctx.strokeText(text, x, y);
  ctx.fillStyle = "#111827";
  ctx.fillText(text, x, y);
}

function roundToHalf(value) {
  return Math.round(value) + 0.5;
}

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

