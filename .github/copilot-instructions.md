# DDClipBot AI Agent Instructions

## Project Architecture

This is a full-stack application with two main components:

1. **Backend (`src/DDClipBot.Host/`)**:
   - ASP.NET Core 8.0 Web API
   - Uses minimal API style with OpenAPI/Swagger documentation
   - Currently implements a basic weather forecast endpoint as template
   - Configuration managed through `appsettings.json` and `appsettings.Development.json`

2. **Frontend (`src/ddclipbot-frontend/`)**:
   - Next.js 14+ application using the App Router
   - TypeScript-based with strict type checking
   - Uses the new App Router architecture (RSC - React Server Components)
   - Implements Geist font family for typography (both Sans and Mono variants)

## Development Workflow

### Backend Development
1. Navigate to `src/DDClipBot.Host/`
2. Run the API locally:
   ```powershell
   dotnet run
   ```
3. Access Swagger UI at `https://localhost:[port]/swagger` in development

### Frontend Development
1. Navigate to `src/ddclipbot-frontend/`
2. Install dependencies:
   ```powershell
   npm install
   ```
3. Start development server:
   ```powershell
   npm run dev
   ```
4. Access the app at `http://localhost:3000`

## Key Files and Patterns

- `src/DDClipBot.Host/Program.cs`: Main API configuration and endpoint definitions
- `src/ddclipbot-frontend/app/layout.tsx`: Root layout with font configuration and metadata
- `src/ddclipbot-frontend/app/page.tsx`: Main page component
- `src/ddclipbot-frontend/app/Components/`: Directory for shared React components

## Project-Specific Conventions

1. **TypeScript Usage**:
   - Strict type checking enabled in `tsconfig.json`
   - Use `Readonly` for props interfaces/types
   - Prefer React Server Components by default (no "use client" unless needed)

2. **API Integration**:
   - Backend uses minimal API style with OpenAPI documentation
   - Endpoints should be documented using `.WithName()` and `.WithOpenApi()`

3. **Styling**:
   - Uses CSS Modules for component-level styling
   - Global styles in `app/globals.css`
   - Implements Geist font family through Next.js font optimization
   - UI styling guide:
        ## 1. Core Aesthetic and Mood
        The UI must convey a sense of **high-tech intensity**, **futurism**, and **powerful energy**. It should feel like an immersive, high-definition gaming interface—**sleek, dynamic, and dramatic**.

        * **Vibe:** Sci-Fi, Neon-Noir, Pop Culture Gaming UI.
        * **Key Descriptors:** Sharp, Sleek, Glowing, High-Contrast, Dynamic.

        ---

        ## 2. Color Palette

        The palette uses stark contrast, blending deep blacks with vibrant, high-energy neon accents inspired by the emblem's digital energy.

        | Color | Hex Code | Usage | Role in UI |
        | :--- | :--- | :--- | :--- |
        | **Primary Base (Stark Dark)** | `#0C0C0C` (or similar deep black/near black) | Backgrounds, Panels, Primary Text. | Provides the "stark, dark, featureless background" for neon elements to pop against. |
        | **Accent Glow (Electric Blue)** | `#00FFFF` (or bright cyan/electric blue) | Primary interactive elements, Loading bars, Active states, Primary text highlights. | Represents the digital energy and high-tech clarity. |
        | **Accent Energy (Neon Green)** | `#39FF14` (or vibrant lime/neon green) | Secondary interactive elements, Success messages, Player status (Online/Ready). | Provides the contrasting "dough energy" color. Used for secondary emphasis. |
        | **Contrast Outline (Neon Orange)** | `#FF4500` (or vibrant neon orange/vermilion) | Call-to-Action (CTA) buttons, Critical alerts, Hover states, Deep volumetric outlines. | The bold, dramatic contrasting color for urgency and deep emphasis. |
        | **Support Text/Data** | `#CCCCCC` (Light Grey) | Body text, Inactive menu items, Data labels. | Ensures legibility against the dark background. |

        ---

        ## 3. Typography

        The typeface must be **clean, angular, and highly legible** to match the high-tech aesthetic.

        * **Primary Font (Headlines/Navigation):** A bold, sharp **sans-serif** with futuristic or geometric qualities (e.g., **Orbitron, Rajdhani, or a similar high-tech font**). Use all **uppercase** for main navigation and headers.
        * **Secondary Font (Body Text/Data):** A highly readable, clean sans-serif (e.g., **Roboto, Inter**) for paragraphs and data display.
        * **Style:** Text should be predominantly **light grey** against the dark background, with key information or active states rendered in **Electric Blue** or **Neon Green**.

        ---

        ## 4. Layout and Structure

        The layout should be **structured, geometric, and clean**, reminiscent of a professional Heads-Up Display (HUD).

        * **Grids:** Use a strong, visible grid system. Elements must snap into place with sharp, clean alignment.
        * **Panels/Containers:** Use **dark, semi-transparent panels** with **1-2 pixel solid borders** of **Electric Blue** or **Neon Orange** for separation. Corners should be **sharp and angular**, or feature minimal (1-2px) rounding.
        * **Information Density:** Maintain a **high level of polish and negative space** (the dark background) to allow key information to breathe and the neon accents to command attention.

        ---

        ## 5. Interaction and Dynamics

        The UI must be **dynamic**, utilizing the concept of **glowing and digital particle effects** from the 'dough energy.'

        * **Hover States:** On hover, an element's outline or text color should shift to a vibrant glow:
            * *Standard:* Light Grey/Blue $\rightarrow$ *Hover:* **Neon Orange** with a subtle, simulated **outer glow**.
        * **Buttons (CTAs):** Primary buttons should be a dark box outlined in **Neon Orange**. On click/active, the button fills with **Electric Blue** and has a subtle **digital particle effect** animation.
        * **Loading/Progress Bars:** Must be animated with a **flowing Electric Blue and Neon Green gradient** and incorporate tiny, fast-moving **light particles** to simulate digital energy transfer.

## Tools and Dependencies

- Backend: .NET 8.0, Swagger/OpenAPI
- Frontend: Next.js 14+, TypeScript, React 18+
- Development: npm/Node.js