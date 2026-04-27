/// <reference types="vite/client" />

import "react";

declare module "react" {
    namespace JSX {
        interface IntrinsicElements {
            "uui-icon": React.DetailedHTMLProps<React.HTMLAttributes<HTMLElement> & { name?: string }, HTMLElement>;
            "uui-tag": React.DetailedHTMLProps<
                React.HTMLAttributes<HTMLElement> & { look?: string; color?: string },
                HTMLElement
            >;
        }
    }
}
