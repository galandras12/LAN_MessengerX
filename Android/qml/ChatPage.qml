import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material
import QtQuick.Layouts
import QtQuick.Dialogs
import LanMessenger 1.0

Page {
    id: page

    property string userId: ""
    property string peerName: ""
    property ChatModel chatModel: messenger.chatModelFor(userId)

    title: peerName

    function formatSize(bytes) {
        if (bytes >= 1073741824) return (bytes / 1073741824).toFixed(2) + " GB"
        if (bytes >= 1048576) return (bytes / 1048576).toFixed(2) + " MB"
        if (bytes >= 1024) return (bytes / 1024).toFixed(2) + " KB"
        return bytes + " bytes"
    }

    function stateLabel(state, progress) {
        switch (state) {
        case "request": return qsTr("Waiting for confirmation…")
        case "transferring": return qsTr("Transferring… %1%").arg(Math.round(progress * 100))
        case "complete": return qsTr("Complete")
        case "declined": return qsTr("Declined")
        case "cancelled": return qsTr("Cancelled")
        case "error": return qsTr("Failed")
        default: return state
        }
    }

    header: ToolBar {
        Material.foreground: "white"
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 16
            Label {
                text: page.peerName
                font.pixelSize: 18
                elide: Text.ElideRight
                Layout.fillWidth: true
            }
        }
    }

    FileDialog {
        id: fileDialog
        title: qsTr("Send a file")
        onAccepted: messenger.sendFile(page.userId, selectedFile)
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        ListView {
            id: messageList
            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            model: page.chatModel
            spacing: 8

            onCountChanged: positionViewAtEnd()

            delegate: Item {
                width: ListView.view.width
                height: (isFile ? fileCard.height : bubble.height) + 8

                //	Text message bubble
                Rectangle {
                    id: bubble
                    visible: !isFile
                    anchors.right: outgoing ? parent.right : undefined
                    anchors.left: outgoing ? undefined : parent.left
                    anchors.margins: 12
                    width: Math.min(label.implicitWidth + 24, page.width * 0.75)
                    height: label.implicitHeight + 16
                    radius: 14
                    color: outgoing ? Material.accentColor : Material.dividerColor

                    Text {
                        id: label
                        anchors.fill: parent
                        anchors.margins: 8
                        text: model.text
                        wrapMode: Text.Wrap
                        color: outgoing ? "white" : Material.foreground
                    }
                }

                //	File transfer card
                Rectangle {
                    id: fileCard
                    visible: isFile
                    anchors.right: outgoing ? parent.right : undefined
                    anchors.left: outgoing ? undefined : parent.left
                    anchors.margins: 12
                    width: Math.min(260, page.width * 0.8)
                    height: fileCardColumn.implicitHeight + 16
                    radius: 14
                    border.width: 1
                    border.color: Material.dividerColor
                    color: Material.background

                    ColumnLayout {
                        id: fileCardColumn
                        anchors.fill: parent
                        anchors.margins: 8
                        spacing: 4

                        RowLayout {
                            Layout.fillWidth: true
                            Label {
                                text: "📎 " + fileName
                                elide: Text.ElideMiddle
                                font.bold: true
                                Layout.fillWidth: true
                            }
                        }

                        Label {
                            text: page.formatSize(fileSize)
                            opacity: 0.6
                            font.pixelSize: 12
                        }

                        ProgressBar {
                            Layout.fillWidth: true
                            visible: state === "transferring"
                            value: progress
                        }

                        Label {
                            text: page.stateLabel(state, progress)
                            font.pixelSize: 12
                            opacity: 0.8
                        }

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 8

                            //	Incoming request: accept/decline
                            Button {
                                text: qsTr("Accept")
                                visible: state === "request" && !outgoing
                                onClicked: messenger.acceptFile(page.userId, fileId)
                            }
                            Button {
                                text: qsTr("Decline")
                                visible: state === "request" && !outgoing
                                onClicked: messenger.declineFile(page.userId, fileId)
                            }

                            //	In-flight (either direction): cancel
                            Button {
                                text: qsTr("Cancel")
                                visible: state === "request" || state === "transferring"
                                onClicked: messenger.cancelFile(page.userId, fileId)
                            }
                        }
                    }
                }
            }
        }

        RowLayout {
            Layout.fillWidth: true
            Layout.margins: 8

            ToolButton {
                text: "📎"
                ToolTip.visible: hovered
                ToolTip.text: qsTr("Send a file")
                onClicked: fileDialog.open()
            }

            TextField {
                id: input
                Layout.fillWidth: true
                placeholderText: qsTr("Message…")
                onAccepted: sendButton.clicked()
            }

            Button {
                id: sendButton
                text: qsTr("Send")
                enabled: input.text.length > 0
                onClicked: {
                    messenger.sendMessage(page.userId, input.text)
                    input.text = ""
                }
            }
        }
    }
}
