import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";

const BASE_URL = process.env.BASE_URL;
const API_KEY = process.env.API_KEY;
const MODEL = process.env.MODEL || "deepseek-v4-flash";
const DEFAULT_EFFORT = process.env.DEFAULT_EFFORT || "xhigh";

if (!BASE_URL || !API_KEY) {
  console.error("Missing BASE_URL or API_KEY");
  process.exit(1);
}

function getEffortConfig(effort) {
  switch (effort) {
    case "low":
      return {
        system:
          "Respond concisely. Do only the minimum reasoning needed. Prefer short answers.",
        temperature: 0.3,
        max_tokens: 1200
      };
    case "high":
      return {
        system:
          "Work carefully. Check assumptions, edge cases, and consistency. Keep the final answer concise.",
        temperature: 0.15,
        max_tokens: 3000
      };
    case "xhigh":
      return {
        system:
          "Work very carefully. Think through assumptions, edge cases, ambiguities, and internal consistency before answering. Prefer correctness over speed. Keep the final answer concise and well-structured.",
        temperature: 0.1,
        max_tokens: 5000
      };
    case "medium":
    default:
      return {
        system: "Work carefully and keep the answer concise.",
        temperature: 0.2,
        max_tokens: 2000
      };
  }
}

const server = new Server(
  {
    name: "deepseek-worker",
    version: "1.0.0"
  },
  {
    capabilities: {
      tools: {}
    }
  }
);

server.tool(
  "ask_worker",
  "Send a bounded, low-risk text task to a cheaper model worker.",
  {
    prompt: z.string().min(1),
    system: z.string().optional(),
    effort: z.enum(["low", "medium", "high", "xhigh"]).optional()
  },
  async ({ prompt, system, effort }) => {
    const effortConfig = getEffortConfig(effort || DEFAULT_EFFORT);
    const mergedSystem = system
      ? `${effortConfig.system}\n\nAdditional instructions:\n${system}`
      : effortConfig.system;

    const body = {
      model: MODEL,
      messages: [
        { role: "system", content: mergedSystem },
        { role: "user", content: prompt }
      ],
      temperature: effortConfig.temperature,
      max_tokens: effortConfig.max_tokens
    };

    const response = await fetch(`${BASE_URL}/chat/completions`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${API_KEY}`
      },
      body: JSON.stringify(body)
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Worker API error ${response.status}: ${errorText}`);
    }

    const data = await response.json();
    const text = data?.choices?.[0]?.message?.content ?? "(no output)";

    return {
      content: [
        {
          type: "text",
          text
        }
      ]
    };
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);
