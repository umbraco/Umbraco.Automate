import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import {
    UMB_VALIDATION_CONTEXT,
    UMB_VALIDATION_EMPTY_LOCALIZATION_KEY,
    type UmbValidator,
} from "@umbraco-cms/backoffice/validation";

/**
 * Reports an empty required value for editors that don't validate `mandatory` themselves.
 *
 * Like UmbValueValidator, but it writes "client" messages. A form control bound to the same
 * path with UmbBindServerValidationToFormControl (such as the step name input, which shares
 * `$.name` with a settings field called `name`) renders every message type except "client",
 * so a "custom" message would show up under that unrelated input.
 */
export class UaRequiredFieldValidator extends UmbControllerBase implements UmbValidator {
    #context?: typeof UMB_VALIDATION_CONTEXT.TYPE;
    #validationMode = false;
    #dataPath: string;
    #value?: unknown;

    isValid = false;

    constructor(host: UmbControllerHost, dataPath: string) {
        super(host);
        this.#dataPath = dataPath;

        this.consumeContext(UMB_VALIDATION_CONTEXT, (context) => {
            if (this.#context === context) return;
            this.#context?.removeValidator(this);
            this.#context = context;
            this.#context?.addValidator(this);
            this.#runCheck();
        });
    }

    set value(value: unknown) {
        this.#value = value;
        this.#runCheck();
    }

    #runCheck() {
        if (!this.#validationMode) return;

        this.isValid = !(this.#value === undefined || this.#value === null || this.#value === "");
        if (!this.#context) return;

        if (this.isValid) {
            this.#context.messages.removeMessagesByTypeAndPath("client", this.#dataPath);
        } else {
            this.#context.messages.addMessage("client", this.#dataPath, UMB_VALIDATION_EMPTY_LOCALIZATION_KEY);
        }
    }

    async validate(): Promise<void> {
        this.#validationMode = true;
        this.#runCheck();
    }

    reset(): void {
        this.isValid = false;
        this.#validationMode = false;
    }

    focusFirstInvalidElement(): void {}

    override destroy(): void {
        if (!this.isValid && this.#validationMode) {
            this.#context?.messages.removeMessagesByTypeAndPath("client", this.#dataPath);
        }
        this.#context?.removeValidator(this);
        this.#context = undefined;
        super.destroy();
    }
}
