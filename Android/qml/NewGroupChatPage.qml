import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material
import QtQuick.Layouts
import LanMessenger 1.0

Page {
    id: page
    title: qsTr("New Group Chat")

    property var selectedIds: []

    header: ToolBar {
        Material.foreground: "white"
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 4

            ToolButton {
                text: "←"
                onClicked: StackView.view.pop()
            }
            Label {
                text: page.title
                font.pixelSize: 18
                elide: Text.ElideRight
                Layout.fillWidth: true
            }
            ToolButton {
                text: qsTr("Create")
                enabled: page.selectedIds.length > 0
                onClicked: {
                    var threadId = messenger.createGroupChat(page.selectedIds)
                    StackView.view.pop()
                    StackView.view.push(Qt.resolvedUrl("GroupChatPage.qml"), {"threadId": threadId})
                }
            }
        }
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        Label {
            text: qsTr("Select people to invite")
            opacity: 0.6
            font.pixelSize: 12
            Layout.fillWidth: true
            Layout.margins: 12
        }

        ListView {
            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            model: messenger.contacts

            delegate: CheckDelegate {
                width: ListView.view.width
                text: name

                onCheckedChanged: {
                    var ids = page.selectedIds.slice()
                    var idx = ids.indexOf(userId)
                    if(checked && idx === -1)
                        ids.push(userId)
                    else if(!checked && idx !== -1)
                        ids.splice(idx, 1)
                    page.selectedIds = ids
                }
            }
        }
    }
}
