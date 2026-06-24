import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import {
  Search,
  Plus,
  Copy,
  Trash2,
  Save,
  CheckCircle2,
  Play,
  Pause,
  Square,
  AlertTriangle,
  GripVertical,
  Pencil,
  CircleDot,
  ChevronDown,
  Activity,
  Bot,
  Clock,
  ShieldCheck,
  RotateCcw,
  Info,
} from "lucide-react";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: "Workflow — NewAGV" },
      { name: "description", content: "Build and execute AGV workflows from SEER TaskChains." },
    ],
  }),
  component: WorkflowPage,
});

type WorkflowStatus = "draft" | "ready" | "running" | "paused" | "error" | "done";

const STATUS_STYLES: Record<WorkflowStatus, string> = {
  draft: "bg-muted text-muted-foreground border-border",
  ready: "bg-accent text-accent-foreground border-primary/30",
  running: "bg-success/15 text-success border-success/40",
  paused: "bg-warning/20 text-warning-foreground border-warning/50",
  error: "bg-destructive/10 text-destructive border-destructive/40",
  done: "bg-secondary text-secondary-foreground border-border",
};

const STATUS_LABEL: Record<WorkflowStatus, string> = {
  draft: "Draft",
  ready: "Ready",
  running: "Running",
  paused: "Paused",
  error: "Error",
  done: "Done",
};

const WORKFLOWS: {
  id: string;
  name: string;
  steps: number;
  updated: string;
  status: WorkflowStatus;
}[] = [
  { id: "wf-021", name: "Inbound Pallet → Rack A", steps: 6, updated: "2m ago", status: "running" },
  { id: "wf-018", name: "Charge & Standby Cycle", steps: 3, updated: "14m ago", status: "ready" },
  { id: "wf-014", name: "Line 3 Replenishment", steps: 8, updated: "1h ago", status: "ready" },
  { id: "wf-012", name: "QA Reject Return", steps: 4, updated: "3h ago", status: "error" },
  { id: "wf-009", name: "Night Shift Sweep", steps: 12, updated: "yesterday", status: "draft" },
  { id: "wf-007", name: "Dock 2 Outbound", steps: 5, updated: "yesterday", status: "done" },
  { id: "wf-004", name: "Empty Bin Return", steps: 2, updated: "2d ago", status: "draft" },
];

const STEPS = [
  {
    n: 1,
    name: "MoveTo: Pickup Station P-12",
    chain: "TC.MoveToPoint",
    status: "done" as WorkflowStatus,
    timeout: 60,
    retry: 2,
    onFail: "Retry then halt",
  },
  {
    n: 2,
    name: "Lift Pallet (Jack Up 80mm)",
    chain: "TC.JackLoad",
    status: "done" as WorkflowStatus,
    timeout: 30,
    retry: 1,
    onFail: "Halt",
  },
  {
    n: 3,
    name: "MoveTo: Aisle A-04 Drop",
    chain: "TC.MoveToPoint",
    status: "running" as WorkflowStatus,
    timeout: 120,
    retry: 2,
    onFail: "Retry then halt",
  },
  {
    n: 4,
    name: "Lower Pallet (Jack Down)",
    chain: "TC.JackUnload",
    status: "draft" as WorkflowStatus,
    timeout: 30,
    retry: 1,
    onFail: "Halt",
  },
  {
    n: 5,
    name: "Return to Standby S-01",
    chain: "TC.ReturnHome",
    status: "draft" as WorkflowStatus,
    timeout: 90,
    retry: 0,
    onFail: "Continue",
  },
];

const HISTORY: {
  time: string;
  workflow: string;
  robot: string;
  step: string;
  result: WorkflowStatus;
  message: string;
}[] = [
  { time: "14:32:08", workflow: "Inbound Pallet → Rack A", robot: "AGV-07", step: "3 / 6 MoveTo A-04", result: "running", message: "Navigating, 62% path complete" },
  { time: "14:31:44", workflow: "Inbound Pallet → Rack A", robot: "AGV-07", step: "2 / 6 JackLoad", result: "done", message: "Lift complete, load detected 412kg" },
  { time: "14:30:51", workflow: "Inbound Pallet → Rack A", robot: "AGV-07", step: "1 / 6 MoveToPoint", result: "done", message: "Arrived at P-12 (±8mm)" },
  { time: "14:28:10", workflow: "Charge & Standby Cycle", robot: "AGV-03", step: "3 / 3 ReturnHome", result: "done", message: "Docked, charging at 11.4kW" },
  { time: "14:21:02", workflow: "QA Reject Return", robot: "AGV-05", step: "2 / 4 JackUnload", result: "error", message: "Obstacle detected, retry exhausted" },
  { time: "14:18:33", workflow: "Line 3 Replenishment", robot: "AGV-02", step: "8 / 8 ReturnHome", result: "done", message: "Workflow completed in 6m 12s" },
  { time: "14:09:17", workflow: "Dock 2 Outbound", robot: "AGV-04", step: "5 / 5 ReturnHome", result: "done", message: "Workflow completed in 4m 02s" },
];

function Chip({
  children,
  tone = "default",
  className = "",
}: {
  children: React.ReactNode;
  tone?: "default" | "primary" | "success" | "warning" | "danger" | "muted";
  className?: string;
}) {
  const tones: Record<string, string> = {
    default: "bg-card border-border text-foreground",
    primary: "bg-accent border-primary/30 text-accent-foreground",
    success: "bg-success/15 border-success/40 text-success",
    warning: "bg-warning/20 border-warning/50 text-warning-foreground",
    danger: "bg-destructive/10 border-destructive/40 text-destructive",
    muted: "bg-muted border-border text-muted-foreground",
  };
  return (
    <span
      className={`inline-flex items-center gap-1.5 h-6 px-2 rounded border text-[11px] font-medium tracking-tight ${tones[tone]} ${className}`}
    >
      {children}
    </span>
  );
}

function StatusChip({ status }: { status: WorkflowStatus }) {
  return (
    <span
      className={`inline-flex items-center gap-1 h-5 px-1.5 rounded border text-[10px] font-semibold uppercase tracking-wide ${STATUS_STYLES[status]}`}
    >
      {status === "running" && <CircleDot className="w-2.5 h-2.5 animate-pulse" />}
      {STATUS_LABEL[status]}
    </span>
  );
}

function ToolbarButton({
  icon: Icon,
  label,
  variant = "default",
  disabled = false,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  variant?: "default" | "primary" | "success" | "warning" | "danger" | "ghost";
  disabled?: boolean;
}) {
  const styles: Record<string, string> = {
    default: "bg-card border-border text-foreground hover:bg-row-hover",
    primary: "bg-primary border-primary text-primary-foreground hover:brightness-110",
    success: "bg-success border-success text-success-foreground hover:brightness-110",
    warning: "bg-card border-warning/60 text-warning-foreground hover:bg-warning/10",
    danger: "bg-card border-destructive/50 text-destructive hover:bg-destructive/10",
    ghost: "bg-transparent border-transparent text-muted-foreground hover:bg-row-hover",
  };
  return (
    <button
      disabled={disabled}
      className={`inline-flex items-center gap-1.5 h-8 px-3 rounded border text-xs font-medium transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${styles[variant]}`}
    >
      <Icon className="w-3.5 h-3.5" />
      {label}
    </button>
  );
}

function WorkflowPage() {
  const [selected, setSelected] = useState("wf-021");
  const [filter, setFilter] = useState<"all" | WorkflowStatus>("all");
  const [inspectorMode, setInspectorMode] = useState<"workflow" | "step" | "runtime">("step");
  const [selectedStep, setSelectedStep] = useState(3);

  const filtered = WORKFLOWS.filter((w) => filter === "all" || w.status === filter);
  const current = WORKFLOWS.find((w) => w.id === selected) ?? WORKFLOWS[0];

  return (
    <div className="min-h-screen bg-background text-foreground text-[13px]">
      {/* HEADER */}
      <header className="border-b border-border bg-card">
        <div className="px-5 py-3 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-7 h-7 rounded bg-primary/10 border border-primary/30 flex items-center justify-center">
              <Bot className="w-4 h-4 text-primary" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground">
                  NewAGV
                </span>
                <span className="text-muted-foreground/50">/</span>
                <h1 className="text-[15px] font-semibold leading-none">Workflow</h1>
              </div>
              <p className="text-[11.5px] text-muted-foreground mt-0.5">
                Build and execute AGV workflows from SEER TaskChains
              </p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-1.5 text-[11px] text-muted-foreground">
              <span className="relative flex w-2 h-2">
                <span className="absolute inset-0 rounded-full bg-success/50 animate-ping" />
                <span className="relative w-2 h-2 rounded-full bg-success" />
              </span>
              SEER Bridge · 192.168.10.14 · 17ms
            </div>
            <div className="h-5 w-px bg-border" />
            <div className="flex items-center gap-2 text-[11px] text-muted-foreground">
              <ShieldCheck className="w-3.5 h-3.5 text-success" />
              Operator: J. Park
            </div>
          </div>
        </div>

        {/* OPERATIONAL ACTION BAR */}
        <div className="px-5 py-2 border-t border-border bg-panel-muted flex items-center gap-2 flex-wrap">
          <Chip tone="primary">
            <Bot className="w-3 h-3" /> Robot: AGV-07
            <ChevronDown className="w-3 h-3 opacity-60" />
          </Chip>
          <Chip>
            <span className="text-muted-foreground">Workflow:</span>{" "}
            <span className="font-semibold">{current.name}</span>
          </Chip>
          <StatusChip status={current.status} />
          <Chip tone="muted">
            <span className="font-semibold">{STEPS.length}</span> steps
          </Chip>
          <Chip tone="muted">
            <Clock className="w-3 h-3" /> Saved 2m ago
          </Chip>
          <Chip tone="warning">
            <CircleDot className="w-2.5 h-2.5" /> Unsaved changes
          </Chip>

          <div className="flex-1" />

          <div className="flex items-center gap-1.5">
            <ToolbarButton icon={Save} label="Save" variant="default" />
            <ToolbarButton icon={ShieldCheck} label="Validate" />
            <div className="w-px h-5 bg-border mx-1" />
            <ToolbarButton icon={Play} label="Run" variant="success" />
            <ToolbarButton icon={Pause} label="Pause" variant="warning" />
            <ToolbarButton icon={RotateCcw} label="Resume" />
            <ToolbarButton icon={Square} label="Cancel" variant="danger" />
          </div>
        </div>
      </header>

      {/* MAIN 3-COLUMN WORKSPACE */}
      <main className="grid grid-cols-[280px_1fr_340px] gap-0 border-b border-border">
        {/* LEFT: CATALOG */}
        <aside className="border-r border-border bg-panel flex flex-col h-[calc(100vh-380px)] min-h-[520px]">
          <div className="p-3 border-b border-border">
            <div className="flex items-center justify-between mb-2">
              <h2 className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                Workflows
              </h2>
              <span className="text-[10px] text-muted-foreground">{WORKFLOWS.length} total</span>
            </div>
            <div className="relative">
              <Search className="absolute left-2 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted-foreground" />
              <input
                placeholder="Search workflows…"
                className="w-full h-8 pl-7 pr-2 rounded border border-input bg-card text-[12px] outline-none focus:border-ring focus:ring-2 focus:ring-ring/20"
              />
            </div>
            <div className="flex flex-wrap gap-1 mt-2">
              {(["all", "draft", "ready", "running", "error"] as const).map((f) => (
                <button
                  key={f}
                  onClick={() => setFilter(f)}
                  className={`h-6 px-2 rounded border text-[10.5px] font-medium uppercase tracking-wide transition-colors ${
                    filter === f
                      ? "bg-primary border-primary text-primary-foreground"
                      : "bg-card border-border text-muted-foreground hover:bg-row-hover"
                  }`}
                >
                  {f}
                </button>
              ))}
            </div>
          </div>

          <div className="flex-1 overflow-y-auto">
            {filtered.map((w) => (
              <button
                key={w.id}
                onClick={() => setSelected(w.id)}
                className={`w-full text-left px-3 py-2 border-b border-border/60 transition-colors ${
                  selected === w.id
                    ? "bg-row-selected border-l-2 border-l-primary"
                    : "hover:bg-row-hover border-l-2 border-l-transparent"
                }`}
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0 flex-1">
                    <div className="text-[12.5px] font-medium truncate">{w.name}</div>
                    <div className="text-[10.5px] text-muted-foreground mt-0.5 font-mono">
                      {w.id} · {w.steps} steps · {w.updated}
                    </div>
                  </div>
                  <StatusChip status={w.status} />
                </div>
              </button>
            ))}
          </div>

          <div className="border-t border-border p-2 flex items-center gap-1 bg-panel-muted">
            <button className="flex-1 h-7 px-2 rounded border border-primary bg-primary text-primary-foreground text-[11px] font-medium inline-flex items-center justify-center gap-1 hover:brightness-110">
              <Plus className="w-3 h-3" /> New
            </button>
            <button className="h-7 px-2 rounded border border-border bg-card text-[11px] inline-flex items-center gap-1 hover:bg-row-hover">
              <Copy className="w-3 h-3" /> Duplicate
            </button>
            <button className="h-7 w-7 rounded border border-border bg-card inline-flex items-center justify-center text-destructive hover:bg-destructive/10">
              <Trash2 className="w-3 h-3" />
            </button>
          </div>
        </aside>

        {/* CENTER: EDITOR */}
        <section className="bg-panel-muted flex flex-col h-[calc(100vh-380px)] min-h-[520px] overflow-hidden">
          {/* Editor header */}
          <div className="px-5 py-3 bg-card border-b border-border">
            <div className="flex items-start justify-between gap-4">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <h2 className="text-[15px] font-semibold leading-tight">{current.name}</h2>
                  <Chip tone="muted">v1.4</Chip>
                  <Chip tone="muted">
                    <span className="font-mono">{current.id}</span>
                  </Chip>
                </div>
                <p className="text-[11.5px] text-muted-foreground mt-1">
                  Pickup pallet at P-12, transport to rack A-04, return AGV to standby.
                </p>
              </div>
              <div className="flex flex-col items-end gap-1 text-[11px] text-muted-foreground shrink-0">
                <div className="flex items-center gap-1.5">
                  <Bot className="w-3 h-3" /> Assigned: <span className="font-semibold text-foreground">AGV-07</span>
                </div>
                <div>Last edited 14:31 by J. Park</div>
              </div>
            </div>
          </div>

          {/* Validation strip */}
          <div className="px-5 py-2 bg-warning/10 border-b border-warning/40 flex items-center gap-3 text-[12px]">
            <AlertTriangle className="w-4 h-4 text-warning-foreground shrink-0" />
            <div className="flex-1">
              <span className="font-semibold text-warning-foreground">2 warnings · 0 errors.</span>{" "}
              <span className="text-muted-foreground">
                Step 5 has retry=0 but onFail=Continue. Step 3 timeout exceeds robot path budget.
              </span>
            </div>
            <button className="text-[11px] font-medium text-primary hover:underline">View details</button>
            <button className="h-6 px-2 rounded border border-border bg-card text-[11px] hover:bg-row-hover">
              Re-validate
            </button>
          </div>

          {/* Canvas */}
          <div className="flex-1 overflow-y-auto px-5 py-4">
            <div className="max-w-[680px] mx-auto">
              {STEPS.map((s, i) => (
                <div key={s.n}>
                  <button
                    onClick={() => {
                      setSelectedStep(s.n);
                      setInspectorMode("step");
                    }}
                    className={`w-full text-left bg-card border rounded shadow-[0_1px_2px_rgba(15,23,42,0.04)] hover:border-primary/50 transition-colors ${
                      selectedStep === s.n ? "border-primary ring-2 ring-primary/15" : "border-border"
                    }`}
                  >
                    <div className="flex items-stretch">
                      <div className="flex items-center justify-center w-8 border-r border-border bg-panel-muted text-muted-foreground hover:text-foreground cursor-grab">
                        <GripVertical className="w-4 h-4" />
                      </div>
                      <div className="flex items-center justify-center w-10 border-r border-border bg-panel-muted">
                        <span className="text-[13px] font-mono font-semibold text-muted-foreground">
                          {String(s.n).padStart(2, "0")}
                        </span>
                      </div>
                      <div className="flex-1 px-3 py-2.5 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          <span className="text-[13px] font-medium truncate">{s.name}</span>
                          <StatusChip status={s.status} />
                        </div>
                        <div className="flex items-center gap-3 text-[11px] text-muted-foreground">
                          <span className="font-mono">{s.chain}</span>
                          <span className="w-1 h-1 rounded-full bg-border" />
                          <span>timeout {s.timeout}s</span>
                          <span className="w-1 h-1 rounded-full bg-border" />
                          <span>retry ×{s.retry}</span>
                          <span className="w-1 h-1 rounded-full bg-border" />
                          <span>{s.onFail}</span>
                        </div>
                      </div>
                      <div className="flex items-center gap-0.5 px-2 border-l border-border">
                        <button className="w-7 h-7 rounded hover:bg-row-hover inline-flex items-center justify-center text-muted-foreground hover:text-foreground">
                          <Pencil className="w-3.5 h-3.5" />
                        </button>
                        <button className="w-7 h-7 rounded hover:bg-row-hover inline-flex items-center justify-center text-muted-foreground hover:text-foreground">
                          <Copy className="w-3.5 h-3.5" />
                        </button>
                        <button className="w-7 h-7 rounded hover:bg-destructive/10 inline-flex items-center justify-center text-muted-foreground hover:text-destructive">
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>
                  </button>

                  {/* Connector + insert affordance */}
                  {i < STEPS.length - 1 && (
                    <div className="relative flex items-center justify-center h-6 group">
                      <div className="w-px h-full bg-border" />
                      <button className="absolute opacity-0 group-hover:opacity-100 transition-opacity h-5 px-2 rounded-full border border-dashed border-primary bg-card text-[10px] font-medium text-primary inline-flex items-center gap-1">
                        <Plus className="w-3 h-3" /> Insert step
                      </button>
                    </div>
                  )}
                </div>
              ))}

              {/* Add step */}
              <button className="mt-3 w-full h-10 rounded border border-dashed border-border bg-card hover:border-primary hover:bg-accent/50 text-[12px] font-medium text-muted-foreground hover:text-primary inline-flex items-center justify-center gap-2 transition-colors">
                <Plus className="w-4 h-4" /> Add TaskChain step
              </button>
            </div>
          </div>
        </section>

        {/* RIGHT: INSPECTOR */}
        <aside className="border-l border-border bg-panel flex flex-col h-[calc(100vh-380px)] min-h-[520px]">
          {/* Mode tabs */}
          <div className="grid grid-cols-3 border-b border-border bg-panel-muted">
            {(
              [
                { k: "workflow", label: "Workflow" },
                { k: "step", label: "Step" },
                { k: "runtime", label: "Runtime" },
              ] as const
            ).map((t) => (
              <button
                key={t.k}
                onClick={() => setInspectorMode(t.k)}
                className={`h-9 text-[11.5px] font-semibold uppercase tracking-wider transition-colors border-b-2 ${
                  inspectorMode === t.k
                    ? "border-primary text-primary bg-card"
                    : "border-transparent text-muted-foreground hover:text-foreground"
                }`}
              >
                {t.label}
              </button>
            ))}
          </div>

          <div className="flex-1 overflow-y-auto p-4 space-y-4">
            {inspectorMode === "workflow" && <WorkflowProperties />}
            {inspectorMode === "step" && <StepProperties n={selectedStep} />}
            {inspectorMode === "runtime" && <RuntimeMonitor />}
          </div>
        </aside>
      </main>

      {/* BOTTOM: EXECUTION HISTORY */}
      <section className="bg-card">
        <div className="px-5 py-2 border-b border-border flex items-center gap-3">
          <Activity className="w-4 h-4 text-primary" />
          <h2 className="text-[12px] font-semibold uppercase tracking-wider">Execution History</h2>
          <span className="text-[11px] text-muted-foreground">last 24h · 142 events</span>
          <div className="flex-1" />
          <Chip tone="muted">
            <CheckCircle2 className="w-3 h-3 text-success" /> 128 ok
          </Chip>
          <Chip tone="muted">
            <AlertTriangle className="w-3 h-3 text-warning-foreground" /> 9 warn
          </Chip>
          <Chip tone="muted">
            <CircleDot className="w-3 h-3 text-destructive" /> 5 error
          </Chip>
          <button className="h-7 px-2 rounded border border-border text-[11px] hover:bg-row-hover">
            Export CSV
          </button>
        </div>
        <div className="overflow-x-auto max-h-[200px] overflow-y-auto">
          <table className="w-full text-[12px]">
            <thead className="sticky top-0 bg-panel-muted border-b border-border z-10">
              <tr className="text-left text-[10.5px] uppercase tracking-wider text-muted-foreground">
                <th className="px-4 py-2 font-semibold w-24">Time</th>
                <th className="px-4 py-2 font-semibold">Workflow</th>
                <th className="px-4 py-2 font-semibold w-24">Robot</th>
                <th className="px-4 py-2 font-semibold w-56">Step</th>
                <th className="px-4 py-2 font-semibold w-24">Result</th>
                <th className="px-4 py-2 font-semibold">Message</th>
              </tr>
            </thead>
            <tbody className="font-mono text-[11.5px]">
              {HISTORY.map((h, i) => (
                <tr key={i} className="border-b border-border/60 hover:bg-row-hover">
                  <td className="px-4 py-1.5 text-muted-foreground">{h.time}</td>
                  <td className="px-4 py-1.5 font-sans">{h.workflow}</td>
                  <td className="px-4 py-1.5">{h.robot}</td>
                  <td className="px-4 py-1.5 text-muted-foreground">{h.step}</td>
                  <td className="px-4 py-1.5">
                    <StatusChip status={h.result} />
                  </td>
                  <td className="px-4 py-1.5 font-sans text-muted-foreground truncate">{h.message}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

/* ---------- Inspector panels ---------- */

function FieldLabel({ children }: { children: React.ReactNode }) {
  return (
    <label className="block text-[10.5px] font-semibold uppercase tracking-wider text-muted-foreground mb-1">
      {children}
    </label>
  );
}

function TextField({ value, mono = false }: { value: string; mono?: boolean }) {
  return (
    <input
      defaultValue={value}
      className={`w-full h-8 px-2 rounded border border-input bg-card text-[12px] outline-none focus:border-ring focus:ring-2 focus:ring-ring/20 ${
        mono ? "font-mono" : ""
      }`}
    />
  );
}

function Select({ value }: { value: string }) {
  return (
    <div className="relative">
      <select
        defaultValue={value}
        className="w-full h-8 pl-2 pr-7 rounded border border-input bg-card text-[12px] appearance-none outline-none focus:border-ring focus:ring-2 focus:ring-ring/20"
      >
        <option>{value}</option>
      </select>
      <ChevronDown className="absolute right-2 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted-foreground pointer-events-none" />
    </div>
  );
}

function Toggle({ on, label, hint }: { on: boolean; label: string; hint?: string }) {
  const [v, setV] = useState(on);
  return (
    <label className="flex items-start justify-between gap-3 py-1.5 cursor-pointer">
      <div className="min-w-0">
        <div className="text-[12px] font-medium">{label}</div>
        {hint && <div className="text-[11px] text-muted-foreground mt-0.5">{hint}</div>}
      </div>
      <button
        type="button"
        onClick={() => setV(!v)}
        className={`shrink-0 w-8 h-[18px] rounded-full border transition-colors relative ${
          v ? "bg-primary border-primary" : "bg-muted border-border"
        }`}
      >
        <span
          className={`absolute top-[1px] w-3.5 h-3.5 rounded-full bg-card shadow transition-all ${
            v ? "left-[14px]" : "left-[1px]"
          }`}
        />
      </button>
    </label>
  );
}

function WorkflowProperties() {
  return (
    <>
      <div>
        <FieldLabel>Workflow Name</FieldLabel>
        <TextField value="Inbound Pallet → Rack A" />
      </div>
      <div>
        <FieldLabel>Description</FieldLabel>
        <textarea
          defaultValue="Pickup pallet at P-12, transport to rack A-04, return AGV to standby."
          rows={3}
          className="w-full px-2 py-1.5 rounded border border-input bg-card text-[12px] outline-none focus:border-ring focus:ring-2 focus:ring-ring/20 resize-none"
        />
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div>
          <FieldLabel>Assigned Robot</FieldLabel>
          <Select value="AGV-07" />
        </div>
        <div>
          <FieldLabel>Execution Mode</FieldLabel>
          <Select value="Sequential" />
        </div>
      </div>
      <div className="border-t border-border pt-3">
        <FieldLabel>Behavior</FieldLabel>
        <Toggle on={true} label="Stop on failure" hint="Halt workflow on first failed step." />
        <Toggle on={false} label="Manual resume" hint="Operator must resume after pause events." />
        <Toggle on={true} label="Require confirmation" hint="Prompt operator before run." />
      </div>
    </>
  );
}

function StepProperties({ n }: { n: number }) {
  const step = STEPS.find((s) => s.n === n) ?? STEPS[2];
  return (
    <>
      <div className="flex items-center gap-2">
        <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-primary/10 text-primary border border-primary/30">
          STEP {String(step.n).padStart(2, "0")}
        </span>
        <StatusChip status={step.status} />
      </div>
      <div>
        <FieldLabel>Step Label</FieldLabel>
        <TextField value={step.name} />
      </div>
      <div>
        <FieldLabel>TaskChain</FieldLabel>
        <Select value={step.chain} />
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div>
          <FieldLabel>Timeout (s)</FieldLabel>
          <TextField value={String(step.timeout)} mono />
        </div>
        <div>
          <FieldLabel>Retry Count</FieldLabel>
          <TextField value={String(step.retry)} mono />
        </div>
      </div>
      <div>
        <FieldLabel>On Failure</FieldLabel>
        <Select value={step.onFail} />
      </div>
      <div>
        <FieldLabel>Note</FieldLabel>
        <textarea
          rows={2}
          placeholder="Optional operator note…"
          className="w-full px-2 py-1.5 rounded border border-input bg-card text-[12px] outline-none focus:border-ring focus:ring-2 focus:ring-ring/20 resize-none"
        />
      </div>
      <div className="border-t border-border pt-3">
        <FieldLabel>TaskChain Preview</FieldLabel>
        <div className="rounded border border-border bg-panel-muted p-3 space-y-1.5">
          <div className="flex items-center justify-between">
            <span className="text-[12px] font-mono font-semibold">{step.chain}</span>
            <StatusChip status="ready" />
          </div>
          <div className="text-[11px] text-muted-foreground flex items-center gap-1.5">
            <Info className="w-3 h-3" /> Last run: 14:30:12 · success · 38.2s
          </div>
          <div className="text-[11px] text-muted-foreground">
            Note: Path budget 110s. Last 5 runs avg 41.7s.
          </div>
        </div>
      </div>
    </>
  );
}

function RuntimeMonitor() {
  return (
    <>
      <div className="rounded border border-success/40 bg-success/10 p-3">
        <div className="flex items-center justify-between mb-1">
          <span className="text-[11px] font-semibold uppercase tracking-wider text-success">
            Workflow Running
          </span>
          <span className="text-[11px] font-mono text-muted-foreground">02:14 elapsed</span>
        </div>
        <div className="text-[12.5px] font-medium">Inbound Pallet → Rack A</div>
        <div className="text-[11px] text-muted-foreground">on AGV-07 · started 14:30:51</div>
      </div>

      <div>
        <FieldLabel>Active Step</FieldLabel>
        <div className="rounded border border-primary/40 bg-accent/40 p-3">
          <div className="flex items-center justify-between">
            <span className="text-[12px] font-semibold">03 · MoveTo Aisle A-04</span>
            <StatusChip status="running" />
          </div>
          <div className="text-[11px] text-muted-foreground font-mono mt-1">TC.MoveToPoint</div>
        </div>
      </div>

      <div>
        <FieldLabel>Progress</FieldLabel>
        <div className="flex items-center justify-between text-[11px] mb-1">
          <span className="text-muted-foreground">Step 3 of {STEPS.length}</span>
          <span className="font-mono font-semibold">62%</span>
        </div>
        <div className="h-2 rounded-full bg-muted overflow-hidden border border-border">
          <div className="h-full bg-primary" style={{ width: "62%" }} />
        </div>
      </div>

      <div>
        <FieldLabel>Last Event</FieldLabel>
        <div className="rounded border border-border bg-panel-muted p-2.5 space-y-1">
          <div className="text-[11px] font-mono text-muted-foreground">14:32:08</div>
          <div className="text-[12px]">Navigating, 62% path complete (LiDAR clear)</div>
        </div>
      </div>

      <div className="border-t border-border pt-3 grid grid-cols-3 gap-1.5">
        <button className="h-9 rounded border border-warning/60 bg-card text-warning-foreground text-[11px] font-medium inline-flex items-center justify-center gap-1 hover:bg-warning/10">
          <Pause className="w-3.5 h-3.5" /> Pause
        </button>
        <button className="h-9 rounded border border-border bg-card text-[11px] font-medium inline-flex items-center justify-center gap-1 hover:bg-row-hover">
          <RotateCcw className="w-3.5 h-3.5" /> Resume
        </button>
        <button className="h-9 rounded border border-destructive/50 bg-card text-destructive text-[11px] font-medium inline-flex items-center justify-center gap-1 hover:bg-destructive/10">
          <Square className="w-3.5 h-3.5" /> Cancel
        </button>
      </div>
    </>
  );
}
