# shadcn/ui Mapping

How to map Mockdown components to [shadcn/ui](https://ui.shadcn.com/) components. shadcn/ui provides
copy-paste React components built on Radix UI + Tailwind CSS. Components are imported from `@/components/ui/`.

## Component Mapping

| Component      | shadcn/ui Component                          | Import Path                       |
|----------------|----------------------------------------------|-----------------------------------|
| Button         | `<Button>`                                   | `@/components/ui/button`          |
| Input          | `<Input placeholder="...">`                  | `@/components/ui/input`           |
| Checkbox       | `<Checkbox>` + `<Label>`                     | `@/components/ui/checkbox`        |
| Radio          | `<RadioGroup>` + `<RadioGroupItem>`          | `@/components/ui/radio-group`     |
| Dropdown       | `<Select>` + `<SelectTrigger/Content/Item>`  | `@/components/ui/select`          |
| Search         | `<CommandInput>` inside `<Command>`          | `@/components/ui/command`         |
| Toggle         | `<Switch>`                                   | `@/components/ui/switch`          |
| Progress Bar   | `<Progress value={60}>`                      | `@/components/ui/progress`        |
| Nav Bar        | `<NavigationMenu>` + items                   | `@/components/ui/navigation-menu` |
| Tabs           | `<Tabs>` + `<TabsList/Trigger/Content>`      | `@/components/ui/tabs`            |
| Breadcrumb     | `<Breadcrumb>` + `<BreadcrumbItem/Link>`     | `@/components/ui/breadcrumb`      |
| Pagination     | `<Pagination>` + `<PaginationItem/Link>`     | `@/components/ui/pagination`      |
| Card           | `<Card>` + `<CardHeader/Content/Footer>`     | `@/components/ui/card`            |
| Dialog / Modal | `<Dialog>` + `<DialogTrigger/Content>`       | `@/components/ui/dialog`          |
| Split Panel    | `<ResizablePanelGroup>` + `<ResizablePanel>` | `@/components/ui/resizable`       |
| Table          | `<Table>` + `<TableHeader/Body/Row/Cell>`    | `@/components/ui/table`           |
| List           | Plain `<ul>` with Tailwind spacing           | No shadcn component; use HTML     |
| Box            | `<Card>` without header, or plain `<div>`    | `@/components/ui/card`            |
| Placeholder    | `<div>` with `<Image>` icon from Lucide      | No shadcn component; use Tailwind |
| Text           | HTML headings/paragraphs with Tailwind       | No shadcn component; use HTML     |
| Line           | `<Separator>`                                | `@/components/ui/separator`       |

## Layout Patterns

### Card with form

```tsx
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"

export function LoginCard() {
  return (
    <Card className="w-[400px]">
      <CardHeader>
        <CardTitle>Login</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="space-y-2">
          <Label htmlFor="email">Email</Label>
          <Input id="email" placeholder="Enter email..." />
        </div>
        <div className="space-y-2">
          <Label htmlFor="password">Password</Label>
          <Input id="password" type="password" />
        </div>
        <div className="flex items-center gap-2">
          <Checkbox id="remember" />
          <Label htmlFor="remember">Remember me</Label>
        </div>
      </CardContent>
      <CardFooter>
        <Button className="w-full">Sign In</Button>
      </CardFooter>
    </Card>
  )
}
```

### Tabs

```tsx
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs"

<Tabs defaultValue="tab1">
  <TabsList>
    <TabsTrigger value="tab1">Tab 1</TabsTrigger>
    <TabsTrigger value="tab2">Tab 2</TabsTrigger>
    <TabsTrigger value="tab3">Tab 3</TabsTrigger>
  </TabsList>
  <TabsContent value="tab1">Content for Tab 1</TabsContent>
  <TabsContent value="tab2">Content for Tab 2</TabsContent>
  <TabsContent value="tab3">Content for Tab 3</TabsContent>
</Tabs>
```

### Table with data

```tsx
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@/components/ui/table"

<Table>
  <TableHeader>
    <TableRow>
      <TableHead>Name</TableHead>
      <TableHead>Role</TableHead>
      <TableHead>Status</TableHead>
    </TableRow>
  </TableHeader>
  <TableBody>
    <TableRow>
      <TableCell>Alice</TableCell>
      <TableCell>Admin</TableCell>
      <TableCell>Active</TableCell>
    </TableRow>
  </TableBody>
</Table>
```

### Dialog

```tsx
import {
  Dialog, DialogTrigger, DialogContent, DialogHeader,
  DialogTitle, DialogFooter, DialogClose
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"

<Dialog>
  <DialogTrigger asChild>
    <Button>Open</Button>
  </DialogTrigger>
  <DialogContent>
    <DialogHeader>
      <DialogTitle>Dialog Title</DialogTitle>
    </DialogHeader>
    <div className="py-4">Dialog body content</div>
    <DialogFooter>
      <DialogClose asChild>
        <Button variant="outline">Cancel</Button>
      </DialogClose>
      <Button>OK</Button>
    </DialogFooter>
  </DialogContent>
</Dialog>
```

### Breadcrumb

```tsx
import {
  Breadcrumb, BreadcrumbList, BreadcrumbItem,
  BreadcrumbLink, BreadcrumbSeparator, BreadcrumbPage
} from "@/components/ui/breadcrumb"

<Breadcrumb>
  <BreadcrumbList>
    <BreadcrumbItem>
      <BreadcrumbLink href="/">Home</BreadcrumbLink>
    </BreadcrumbItem>
    <BreadcrumbSeparator />
    <BreadcrumbItem>
      <BreadcrumbLink href="/section">Section</BreadcrumbLink>
    </BreadcrumbItem>
    <BreadcrumbSeparator />
    <BreadcrumbItem>
      <BreadcrumbPage>Page</BreadcrumbPage>
    </BreadcrumbItem>
  </BreadcrumbList>
</Breadcrumb>
```

## Button Variants

Map wireframe button context to the appropriate variant:

| Wireframe Context                   | Variant       |
|-------------------------------------|---------------|
| Primary action (CTA, Submit, OK)    | `default`     |
| Secondary action (Cancel, Back)     | `outline`     |
| Destructive action (Delete, Remove) | `destructive` |
| Subtle action (Edit, More)          | `ghost`       |
| Nav bar link-style button           | `link`        |
