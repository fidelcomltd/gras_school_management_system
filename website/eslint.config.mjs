import { defineConfig, globalIgnores } from "eslint/config"
import nextVitals from "eslint-config-next/core-web-vitals"
import nextTs from "eslint-config-next/typescript"

export default defineConfig([
  ...nextVitals,
  ...nextTs,
  {
    rules: {
      // Links come from config/routes.ts and site config, never typed inline (AGENTS.md rule 4).
      "no-restricted-syntax": [
        "error",
        {
          selector: "JSXAttribute[name.name='href'][value.type='Literal'][value.value=/^\\x2F/]",
          message: "Use a route from config/routes.ts instead of a hard-coded path.",
        },
      ],
    },
  },
  globalIgnores([".next/**", "node_modules/**", "playwright-report/**", "test-results/**", "next-env.d.ts"]),
])
