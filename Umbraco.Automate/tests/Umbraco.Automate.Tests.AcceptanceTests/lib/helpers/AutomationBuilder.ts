import { randomUUID } from 'crypto';
import { ConstantHelper } from './ConstantHelper';

/** A step as the management API takes it (StepConfigurationModel). */
export type AutomationStep = {
  id: string;
  actionAlias: string;
  name: string;
  alias: string;
  connectionId: string | null;
  settings: Record<string, unknown>;
  inputMappings: Record<string, unknown>;
  position: { x: number; y: number };
  errorBehavior: 'Terminate' | 'Retry' | 'Suspend' | 'Compensate';
  retryInterval: string | null;
  maxRetries: number | null;
};

/** A connection between two steps, as the management API takes it (StepConnectionModel). */
export type AutomationConnection = {
  sourceStepId: string;
  sourceHandle: string | null;
  targetStepId: string;
  targetHandle: string | null;
  outcome: string | null;
  filter: null;
};

/**
 * Builds a step for seeding an automation through the API.
 *
 * The **alias** is how a spec finds the step again. The server does not keep the id sent on
 * create — it assigns its own and rewires the connections — so read the automation back and
 * look steps up with `AutomationApiHelper.stepByAlias` rather than holding on to `step.id`.
 * The id is still needed here, to wire the connections in the same request.
 */
export function automationStep(
  actionAlias: string,
  alias: string,
  settings: Record<string, unknown> = {},
  position: { x: number; y: number } = { x: 250, y: 250 }
): AutomationStep {
  return {
    id: randomUUID(),
    actionAlias,
    name: alias,
    alias,
    connectionId: null,
    settings,
    inputMappings: {},
    position,
    errorBehavior: 'Terminate',
    retryInterval: null,
    maxRetries: null
  };
}

/**
 * Connects two steps. Pass `'trigger'` as the source for the trigger's output, which the API
 * represents as the empty Guid. `sourceHandle` is the branch (`true`/`false`, `approved`/
 * `rejected`, `body`/`done`, a Switch case); the canvas saves it as the outcome too.
 */
export function automationConnection(
  source: AutomationStep | 'trigger',
  target: AutomationStep,
  sourceHandle: string | null = null
): AutomationConnection {
  return {
    sourceStepId: source === 'trigger' ? ConstantHelper.emptyGuid : source.id,
    sourceHandle,
    targetStepId: target.id,
    targetHandle: null,
    outcome: sourceHandle,
    filter: null
  };
}

/** The Manual trigger — runnable on demand and with no settings, so the simplest to publish. */
export function manualTrigger() {
  return { triggerAlias: ConstantHelper.triggers.manual, settings: {} };
}
