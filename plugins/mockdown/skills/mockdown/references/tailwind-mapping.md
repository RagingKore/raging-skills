# Tailwind CSS + Lucide Icons Mapping

How to map Mockdown components to a polished, self-contained HTML file using Tailwind CSS and Lucide Icons via
CDN. Zero build step; produces a single `.html` file that looks good out of the box.

## CDN Setup

Include these in the `<head>` of the generated HTML:

```html
<script src="https://cdn.tailwindcss.com"></script>
<script src="https://unpkg.com/lucide@latest"></script>
```

Initialize Lucide icons at the end of `<body>`:

```html
<script>lucide.createIcons();</script>
```

## Component Mapping

| Component      | Tailwind Implementation                                                 | Lucide Icon             |
|----------------|-------------------------------------------------------------------------|-------------------------|
| Button         | `<button class="px-4 py-2 bg-blue-600 text-white rounded-lg">`          | —                       |
| Input          | `<input class="border rounded-lg px-3 py-2 w-full">`                    | —                       |
| Checkbox       | `<input type="checkbox" class="rounded">` + `<label>`                   | —                       |
| Radio          | `<input type="radio">` + `<label>`                                      | —                       |
| Dropdown       | `<select class="border rounded-lg px-3 py-2">`                          | `chevron-down`          |
| Search         | `<input>` with icon prefix                                              | `search`                |
| Toggle         | Custom div with `bg-blue-600` / `bg-gray-300` + circle                  | —                       |
| Progress Bar   | Nested divs: outer `bg-gray-200 rounded-full`, inner `bg-blue-600`      | —                       |
| Nav Bar        | `<nav class="flex items-center justify-between px-6 py-4">`             | —                       |
| Tabs           | `<div role="tablist">` with active `border-b-2 border-blue-600`         | —                       |
| Breadcrumb     | `<nav>` with `<ol class="flex items-center gap-2">`                     | `chevron-right` between |
| Pagination     | `<nav>` with `flex gap-1`, active page `bg-blue-600 text-white`         | `chevron-left/right`    |
| Card           | `<div class="rounded-xl border shadow-sm">`                             | —                       |
| Dialog / Modal | `<dialog>` or overlay div with `bg-white rounded-xl shadow-xl`          | `x` for close           |
| Split Panel    | `<div class="grid grid-cols-[1fr_2fr]">`                                | —                       |
| Table          | `<table class="w-full text-left">` with `divide-y`                      | —                       |
| List           | `<ul class="space-y-2">` with `<li class="flex items-center">`          | `circle` for bullets    |
| Box            | `<div class="border rounded-lg p-4">`                                   | —                       |
| Placeholder    | `<div class="bg-gray-100 rounded-lg flex items-center justify-center">` | `image`                 |
| Text           | `<h1>`-`<h6>` with `text-2xl font-bold` etc., `<p>` for body            | —                       |
| Line           | `<hr class="border-gray-200">`                                          | —                       |

## Layout Patterns

### Page shell

```html
<div class="min-h-screen bg-gray-50">
  <nav class="bg-white border-b px-6 py-4 flex items-center justify-between">
    <span class="font-bold text-lg">Logo</span>
    <div class="flex gap-6">
      <a href="#" class="text-gray-600 hover:text-gray-900">Link</a>
      <a href="#" class="text-gray-600 hover:text-gray-900">Link</a>
    </div>
    <button class="px-4 py-2 bg-blue-600 text-white rounded-lg">Action</button>
  </nav>
  <main class="max-w-6xl mx-auto p-6">
    <!-- content -->
  </main>
</div>
```

### Side-by-side

```html
<div class="grid grid-cols-2 gap-6">
  <div>Left</div>
  <div>Right</div>
</div>
```

### Card with form

```html
<div class="rounded-xl border bg-white shadow-sm max-w-md">
  <div class="border-b px-6 py-4">
    <h2 class="font-semibold">Login</h2>
  </div>
  <div class="p-6 space-y-4">
    <input type="email" placeholder="Email" class="border rounded-lg px-3 py-2 w-full">
    <input type="password" placeholder="Password" class="border rounded-lg px-3 py-2 w-full">
    <label class="flex items-center gap-2">
      <input type="checkbox" class="rounded"> Remember me
    </label>
    <button class="w-full px-4 py-2 bg-blue-600 text-white rounded-lg">Sign In</button>
  </div>
</div>
```

### Search input with icon

```html
<div class="relative">
  <i data-lucide="search" class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400"></i>
  <input type="search" placeholder="Search..." class="border rounded-lg pl-10 pr-3 py-2 w-full">
</div>
```

## Styling Conventions

- Use `rounded-lg` for inputs and buttons, `rounded-xl` for cards and modals
- Use `shadow-sm` for cards, `shadow-xl` for modals/dialogs
- Active/selected states: `bg-blue-600 text-white`
- Muted text: `text-gray-500`
- Borders: `border-gray-200`
- Spacing: `gap-4` between form fields, `gap-6` between sections
